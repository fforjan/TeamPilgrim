
namespace JustAProgrammer.TeamPilgrim.VisualStudio.Common.AttachedProperties
{
	using System.Windows.Input;

	public class PreviewKeyDownControlZ : PreviewKeyDown<PreviewKeyDownControlZ.BackspacePlusShiftSelector>
	{
		public class BackspacePlusShiftSelector : IKeyEventSelector
		{
			public bool CanExecute(object sender, KeyEventArgs e)
			{
				return e.Key == Key.Z && System.Windows.Forms.Control.ModifierKeys == System.Windows.Forms.Keys.Control;
			}
		}
	}
}
