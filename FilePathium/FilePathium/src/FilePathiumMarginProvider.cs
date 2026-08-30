using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace FilePathium {

	[Export(typeof(IWpfTextViewMarginProvider))]
	[Name(FilePathiumMargin.MarginName)]
	[Order(After = PredefinedMarginNames.HorizontalScrollBar)]
	[MarginContainer(PredefinedMarginNames.Bottom)]
	[ContentType("text")]
	[TextViewRole(PredefinedTextViewRoles.Interactive)]
	internal sealed class FilePathiumMarginProvider : IWpfTextViewMarginProvider {
		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer) {
			return new FilePathiumMargin(wpfTextViewHost.TextView);
		}
	}

}