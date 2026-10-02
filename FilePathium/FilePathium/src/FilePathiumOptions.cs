using System;
using System.ComponentModel;
using System.Diagnostics;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace FilePathium {

	public enum PathDisplayMode {
		FullPath,
		RelativeToSolution,
		FileNameOnly
	}

	/// <summary>
	/// Options page: Tools > Options > FilePathium > General
	/// </summary>
	public class FilePathiumOptions : DialogPage {

		/// <summary>
		/// Raised on the UI thread after the user pressed OK/Apply in the options dialog.
		/// </summary>
		public static event EventHandler SettingsApplied;

		[Category("Appearance")]
		[DisplayName("Show file path bar")]
		[Description("Shows or hides the file path bar below the text editor.")]
		public bool ShowMargin { get; set; } = true;

		[Category("Appearance")]
		[DisplayName("Font size")]
		[Description("Font size of the file path text (8 - 24). The bar height adapts automatically.")]
		public int FontSize { get; set; } = 12;

		[Category("Appearance")]
		[DisplayName("Show Explorer button")]
		[Description("Shows the button that opens the file location in Windows Explorer.")]
		public bool ShowExplorerButton { get; set; } = true;

		[Category("Behavior")]
		[DisplayName("Path display")]
		[Description("FullPath: complete path. RelativeToSolution: path relative to the solution folder (falls back to the full path). FileNameOnly: only the file name.")]
		public PathDisplayMode PathDisplay { get; set; } = PathDisplayMode.FullPath;

		[Category("Behavior")]
		[DisplayName("Copy path on click")]
		[Description("Clicking the file path copies the full path to the clipboard.")]
		public bool CopyPathOnClick { get; set; } = true;

		protected override void OnApply(PageApplyEventArgs e) {
			base.OnApply(e);

			if(e.ApplyBehavior == ApplyKind.Apply) {
				SettingsApplied?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>
		/// Returns the live options instance (loads the package if necessary).
		/// Falls back to default values if that is not possible.
		/// </summary>
		internal static FilePathiumOptions GetInstance() {
			ThreadHelper.ThrowIfNotOnUIThread();

			try {
				if(Package.GetGlobalService(typeof(SVsShell)) is IVsShell shell) {
					var packageGuid = new Guid(FilePathiumPackage.PackageGuidString);

					if(shell.IsPackageLoaded(ref packageGuid, out IVsPackage package) != VSConstants.S_OK || package == null) {
						shell.LoadPackage(ref packageGuid, out package);
					}

					if(package is Package pkg) {
						return (FilePathiumOptions)pkg.GetDialogPage(typeof(FilePathiumOptions));
					}
				}
			}
			catch(Exception ex) {
				Debug.WriteLine($"FilePathium: could not load options: {ex}");
			}

			return new FilePathiumOptions();
		}
	}
}