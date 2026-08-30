using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace FilePathium {

	/// <summary>
	/// A margin that displays the file path of the current document in the Visual Studio editor.
	/// </summary>
	internal sealed class FilePathiumMargin : Border, IWpfTextViewMargin {
		public const string MarginName = "FilePathiumMargin";
		public const int MarginHeight = 24;
		public const int FontSize = 12;

		private readonly IWpfTextView _textView;
		private readonly TextBlock _filePathText;

		private bool _disposed;

		public FilePathiumMargin(IWpfTextView textView) {
			_textView = textView;

			Height = MarginHeight;

			Background = new SolidColorBrush(Color.FromRgb(37, 37, 38));
			BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60));

			BorderThickness = new Thickness(0, 1, 0, 0);

			// Layout
			var grid = new Grid();

			grid.ColumnDefinitions.Add(
				new ColumnDefinition {
					Width = new GridLength(1, GridUnitType.Star)
				});

			grid.ColumnDefinitions.Add(
				new ColumnDefinition {
					Width = GridLength.Auto
				});

			// File path
			_filePathText = new TextBlock {
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(8, 0, 8, 0),
				FontSize = FontSize,
				Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
				TextTrimming = TextTrimming.CharacterEllipsis
			};

			Grid.SetColumn(_filePathText, 0);
			grid.Children.Add(_filePathText);

			// Explorer button
			
			var explorerButton = new Button {
				Width = 28,
				Height = 22,
				Margin = new Thickness(0, 0, 4, 0),
				Padding = new Thickness(0),
				Background = Brushes.Transparent,
				BorderBrush = Brushes.Transparent,
				BorderThickness = new Thickness(0),
				ToolTip = "Open file location in Explorer",
				Cursor = System.Windows.Input.Cursors.Hand
			};
			// Simple monochrome folder icon
			var folderIcon = new System.Windows.Shapes.Path {
				Width = 14,
				Height = 14,
				Stretch = Stretch.Uniform,
				Fill = new SolidColorBrush( Color.FromRgb(180, 180, 180)),
				Data = Geometry.Parse( "M 1,3 " + "L 6,3 " + "L 8,5 " + "L 13,5 " + "C 13.55,5 14,5.45 14,6 " + "L 14,12 " + "C 14,12.55 13.55,13 13,13 " + "L 1,13 " + "C 0.45,13 0,12.55 0,12 " + "L 0,4 " + "C 0,3.45 0.45,3 1,3 Z")
			};
			explorerButton.Content = folderIcon;
			explorerButton.MouseEnter += (_, _) => {
				folderIcon.Fill = new SolidColorBrush( Color.FromRgb(220, 220, 220));
			};
			explorerButton.MouseLeave += (_, _) => {
				folderIcon.Fill = new SolidColorBrush( Color.FromRgb(180, 180, 180));
			};
			explorerButton.Click += OnExplorerButtonClicked;
			
			Grid.SetColumn(explorerButton, 1);
			grid.Children.Add(explorerButton);

			Child = grid;

			UpdateFilePath();

			_textView.Closed += OnTextViewClosed;
		}

		private void UpdateFilePath() {
			if(_disposed) {
				return;
			}

			if(_textView.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document)) {
				_filePathText.Text = document.FilePath;
			}
			else {
				_filePathText.Text = "Unsaved document";
			}
		}

		private void OnExplorerButtonClicked(object sender, RoutedEventArgs e) {
			if(_disposed) {
				return;
			}

			if(!_textView.TextBuffer.Properties.TryGetProperty(
					typeof(ITextDocument),
					out ITextDocument document)) {
				return;
			}

			string filePath = document.FilePath;

			if(string.IsNullOrWhiteSpace(filePath)) {
				return;
			}

			if(!File.Exists(filePath)) {
				return;
			}

			Process.Start(new ProcessStartInfo {
				FileName = "explorer.exe",
				Arguments = $"/select,\"{filePath}\"",
				UseShellExecute = true
			});
		}

		private void OnTextViewClosed(
			object sender,
			EventArgs e) {
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

		public ITextViewMargin GetTextViewMargin(
			string marginName) {
			ThrowIfDisposed();

			if(string.Equals(
					marginName,
					MarginName,
					StringComparison.OrdinalIgnoreCase)) {
				return this;
			}

			return null;
		}

		public void Dispose() {
			if(_disposed) {
				return;
			}

			_disposed = true;

			_textView.Closed -= OnTextViewClosed;
		}

		private void ThrowIfDisposed() {
			if(_disposed) {
				throw new ObjectDisposedException(MarginName);
			}
		}
	}
}
