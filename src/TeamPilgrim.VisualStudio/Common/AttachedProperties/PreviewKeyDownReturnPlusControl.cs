
namespace JustAProgrammer.TeamPilgrim.VisualStudio.Common.AttachedProperties
{
	using System.Windows.Input;

	public class PreviewKeyDownReturnPlusControl : PreviewKeyDown<PreviewKeyDownReturnPlusControl.ReturnPlusControlSelector>
	{
		public class ReturnPlusControlSelector : IKeyEventSelector
		{
			public bool CanExecute(object sender, KeyEventArgs e)
			{
				return e.Key == Key.Return && System.Windows.Forms.Control.ModifierKeys == System.Windows.Forms.Keys.Control;
			}
		}
	}
}
