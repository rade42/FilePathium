using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace FilePathium {

	[Export(typeof(IWpfTextViewMarginProvider))]
	[Name(FilePathiumMargin.MarginName)]
	[Order(After = PredefinedMarginNames.HorizontalScrollBar)]
	[MarginContainer(PredefinedMarginNames.Bottom)]
	[ContentType("text")]
	[TextViewRole(PredefinedTextViewRoles.Document)]
	internal sealed class FilePathiumMarginProvider : IWpfTextViewMarginProvider {
		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer) {
			ThreadHelper.ThrowIfNotOnUIThread();
			return new FilePathiumMargin(wpfTextViewHost.TextView, FilePathiumOptions.GetInstance());
		}
	}

}