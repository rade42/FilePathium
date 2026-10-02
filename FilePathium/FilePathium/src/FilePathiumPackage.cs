using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace FilePathium {
	/// <summary>
	/// This is the class that implements the package exposed by this assembly.
	/// It registers the options page (Tools > Options > FilePathium > General).
	/// </summary>
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[Guid(FilePathiumPackage.PackageGuidString)]
	[ProvideOptionPage(typeof(FilePathiumOptions), "FilePathium", "General", 0, 0, true)]
	public sealed class FilePathiumPackage : AsyncPackage {
		/// <summary>
		/// FilePathiumPackage GUID string.
		/// </summary>
		public const string PackageGuidString = "6da13ea3-8829-454b-b4f5-87735f137856";

		protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress) {
			await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
		}
	}
}