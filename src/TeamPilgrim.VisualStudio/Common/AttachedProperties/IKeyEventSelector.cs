using System.Windows.Input;

namespace JustAProgrammer.TeamPilgrim.VisualStudio.Common.AttachedProperties
{
	public interface IKeyEventSelector
	{
		bool CanExecute(object sender, KeyEventArgs e);
	}
}
