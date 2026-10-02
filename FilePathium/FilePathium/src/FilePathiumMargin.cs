using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace FilePathium {

	/// <summary>
	/// A margin that displays the file path of the current document in the Visual Studio editor.
	/// </summary>
	internal sealed class FilePathiumMargin : Border, IWpfTextViewMargin {
		public const string MarginName = "FilePathiumMargin";

		private const string UnsavedText = "Unsaved document";
		private const int MinFontSize = 8;
		private const int MaxFontSize = 24;
		private const int CopiedFeedbackMs = 1000;
		private const string CopiedText = "Copied to clipboard";

		private readonly IWpfTextView _textView;
		private readonly FilePathiumOptions _options;
		private readonly TextBlock _filePathText;
		private readonly Button _explorerButton;

		private ITextDocument _document;
		private DispatcherTimer _feedbackTimer;
		private bool _disposed;

		public FilePathiumMargin(IWpfTextView textView, FilePathiumOptions options) {
			_textView = textView;
			_options = options ?? new FilePathiumOptions();

			BorderThickness = new Thickness(0, 1, 0, 0);

			// Theme-aware colors (update automatically when the VS theme changes)
			SetResourceReference(BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);
			SetResourceReference(BorderBrushProperty, EnvironmentColors.ToolWindowBorderBrushKey);

			// Layout
			var grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

			// File path
			_filePathText = new TextBlock {
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(8, 0, 8, 0),
				TextTrimming = TextTrimming.CharacterEllipsis,
				Background = Brushes.Transparent // makes the whole text area clickable
			};
			_filePathText.MouseLeftButtonUp += OnFilePathClicked;
			_filePathText.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);

			Grid.SetColumn(_filePathText, 0);
			grid.Children.Add(_filePathText);

			// Explorer button
			_explorerButton = CreateExplorerButton();
			Grid.SetColumn(_explorerButton, 1);
			grid.Children.Add(_explorerButton);

			Child = grid;

			// Document tracking
			if(_textView.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document)) {
				_document = document;
				_document.FileActionOccurred += OnFileActionOccurred;
			}

			ApplyOptions();

			FilePathiumOptions.SettingsApplied += OnSettingsApplied;
			_textView.Closed += OnTextViewClosed;
		}

		private Button CreateExplorerButton() {
			// Slim template without the default Windows button chrome
			var template = new ControlTemplate(typeof(Button));
			var border = new FrameworkElementFactory(typeof(Border));
			border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") {
				RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
			});
			border.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
			var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
			presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
			presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
			border.AppendChild(presenter);
			template.VisualTree = border;

			var button = new Button {
				Width = 28,
				Height = 22,
				Margin = new Thickness(0, 0, 4, 0),
				Padding = new Thickness(0),
				Background = Brushes.Transparent,
				BorderThickness = new Thickness(0),
				Template = template,
				Focusable = false,
				ToolTip = "Open file location in Explorer",
				Cursor = Cursors.Hand
			};

			// Simple monochrome folder icon
			var folderIcon = new System.Windows.Shapes.Path {
				Width = 14,
				Height = 14,
				Stretch = Stretch.Uniform,
				Data = Geometry.Parse(
					"M 1,3 L 6,3 L 8,5 L 13,5 " +
					"C 13.55,5 14,5.45 14,6 L 14,12 " +
					"C 14,12.55 13.55,13 13,13 L 1,13 " +
					"C 0.45,13 0,12.55 0,12 L 0,4 " +
					"C 0,3.45 0.45,3 1,3 Z")
			};
			folderIcon.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, EnvironmentColors.ToolWindowTextBrushKey);
			button.Content = folderIcon;

			button.MouseEnter += (_, _) =>
				button.SetResourceReference(BackgroundProperty, EnvironmentColors.CommandBarMouseOverBackgroundGradientBrushKey);
			button.MouseLeave += (_, _) => {
				button.ClearValue(BackgroundProperty);
				button.Background = Brushes.Transparent;
			};

			button.Click += OnExplorerButtonClicked;

			return button;
		}

		/// <summary>
		/// Applies the current option values to the UI.
		/// </summary>
		private void ApplyOptions() {
			if(_disposed) {
				return;
			}

			int fontSize = Math.Max(MinFontSize, Math.Min(MaxFontSize, _options.FontSize));

			Visibility = _options.ShowMargin ? Visibility.Visible : Visibility.Collapsed;
			Height = fontSize + 12; // 12 -> 24 px, same as before
			_filePathText.FontSize = fontSize;
			_explorerButton.Visibility = _options.ShowExplorerButton ? Visibility.Visible : Visibility.Collapsed;

			UpdateFilePath();
		}

		private void UpdateFilePath() {
			if(_disposed) {
				return;
			}

			string filePath = _document?.FilePath;

			if(string.IsNullOrWhiteSpace(filePath)) {
				_filePathText.Text = UnsavedText;
				_filePathText.ToolTip = null;
				_filePathText.Cursor = null;
				_explorerButton.IsEnabled = false;
			}
			else {
				_filePathText.Text = GetDisplayPath(filePath);
				_filePathText.ToolTip = _options.CopyPathOnClick
					? filePath + Environment.NewLine + "Click to copy the full path"
					: filePath; // always the full path
				_filePathText.Cursor = _options.CopyPathOnClick ? Cursors.Hand : null;
				_explorerButton.IsEnabled = true;
			}
		}

		private string GetDisplayPath(string filePath) {
			switch(_options.PathDisplay) {
				case PathDisplayMode.FileNameOnly:
					return Path.GetFileName(filePath);

				case PathDisplayMode.RelativeToSolution:
					string solutionDir = GetSolutionDirectory();

					if(!string.IsNullOrEmpty(solutionDir)) {
						solutionDir = solutionDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
							+ Path.DirectorySeparatorChar;

						if(filePath.StartsWith(solutionDir, StringComparison.OrdinalIgnoreCase)) {
							return filePath.Substring(solutionDir.Length);
						}
					}

					return filePath; // no solution or file outside of it

				default:
					return filePath;
			}
		}

		private static string GetSolutionDirectory() {
			ThreadHelper.ThrowIfNotOnUIThread();

			try {
				if(Package.GetGlobalService(typeof(SVsSolution)) is IVsSolution solution
					&& ErrorHandler.Succeeded(solution.GetSolutionInfo(out string directory, out _, out _))) {
					return directory;
				}
			}
			catch(Exception ex) {
				Debug.WriteLine($"{MarginName}: could not get solution directory: {ex}");
			}

			return null;
		}

		private void OnSettingsApplied(object sender, EventArgs e) {
			Dispatcher.InvokeAsync(ApplyOptions);
		}

		private void OnFileActionOccurred(object sender, TextDocumentFileActionEventArgs e) {
			if(e.FileActionType != FileActionTypes.DocumentRenamed) {
				return;
			}

			// The event is not guaranteed to be raised on the UI thread
			Dispatcher.InvokeAsync(UpdateFilePath);
		}

		private void OnFilePathClicked(object sender, MouseButtonEventArgs e) {
			if(_disposed || !_options.CopyPathOnClick) {
				return;
			}

			string filePath = _document?.FilePath;

			if(string.IsNullOrWhiteSpace(filePath)) {
				return;
			}

			try {
				// copy: true keeps the data on the clipboard after VS closes; retries internally if the clipboard is busy
				Clipboard.SetDataObject(filePath, true);
			}
			catch(Exception ex) {
				Debug.WriteLine($"{MarginName}: failed to copy path: {ex}");
				return;
			}

			e.Handled = true;
			ShowCopiedFeedback();
		}

		private void ShowCopiedFeedback() {
			_filePathText.Text = CopiedText;

			if(_feedbackTimer == null) {
				_feedbackTimer = new DispatcherTimer {
					Interval = TimeSpan.FromMilliseconds(CopiedFeedbackMs)
				};
				_feedbackTimer.Tick += (_, _) => {
					_feedbackTimer.Stop();
					UpdateFilePath();
				};
			}

			_feedbackTimer.Stop();
			_feedbackTimer.Start();
		}

		private void OnExplorerButtonClicked(object sender, RoutedEventArgs e) {
			if(_disposed) {
				return;
			}

			string filePath = _document?.FilePath;

			if(string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) {
				return;
			}

			try {
				Process.Start(new ProcessStartInfo {
					FileName = "explorer.exe",
					Arguments = $"/select,\"{filePath}\"",
					UseShellExecute = true
				});
			}
			catch(Exception ex) {
				Debug.WriteLine($"{MarginName}: failed to open Explorer: {ex}");
			}
		}

		private void OnTextViewClosed(object sender, EventArgs e) {
			Dispose();
		}

		// IWpfTextViewMargin

		public FrameworkElement VisualElement {
			get {
				ThrowIfDisposed();
				return this;
			}
		}

		public double MarginSize {
			get {
				ThrowIfDisposed();
				return ActualHeight;
			}
		}

		public bool Enabled {
			get {
				ThrowIfDisposed();
				return true;
			}
		}

		public ITextViewMargin GetTextViewMargin(string marginName) {
			ThrowIfDisposed();

			return string.Equals(marginName, MarginName, StringComparison.OrdinalIgnoreCase)
				? this
				: null;
		}

		public void Dispose() {
			if(_disposed) {
				return;
			}

			_disposed = true;

			FilePathiumOptions.SettingsApplied -= OnSettingsApplied;
			_textView.Closed -= OnTextViewClosed;
			_filePathText.MouseLeftButtonUp -= OnFilePathClicked;
			_feedbackTimer?.Stop();

			if(_document != null) {
				_document.FileActionOccurred -= OnFileActionOccurred;
				_document = null;
			}
		}

		private void ThrowIfDisposed() {
			if(_disposed) {
				throw new ObjectDisposedException(MarginName);
			}
		}
	}
}