﻿using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JustAProgrammer.TeamPilgrim.VisualStudio.Common.AttachedProperties
{
	using System;

	public class PreviewKeyDown<T> where T : IKeyEventSelector, new()
    {
		private static readonly T selector = new T();
 
        public static DependencyProperty CommandProperty =
            DependencyProperty.RegisterAttached("Command",
												typeof(ICommand), typeof(PreviewKeyDown<T>), new UIPropertyMetadata(CommandChanged));

        public static DependencyProperty CommandParameterProperty =
            DependencyProperty.RegisterAttached("CommandParameter",
												typeof(object), typeof(PreviewKeyDown<T>), new UIPropertyMetadata(null));

        public static void SetCommand(DependencyObject target, ICommand value)
        {
            target.SetValue(CommandProperty, value);
        }

        public static void SetCommandParameter(DependencyObject target, object value)
        {
            target.SetValue(CommandParameterProperty, value);
        }

        public static object GetCommandParameter(DependencyObject target)
        {
            return target.GetValue(CommandParameterProperty);
        }

        private static void CommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            var control = target as Control;
            if (control != null)
            {
                if ((e.NewValue != null) && (e.OldValue == null))
                {
                    control.PreviewKeyDown += OnPreviewKeyDown;
                }
                else if ((e.NewValue == null) && (e.OldValue != null))
                {
                    control.PreviewKeyDown -= OnPreviewKeyDown;
                }
            }
        }

		private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (selector.CanExecute(sender, e))
			{
				e.Handled = true;

				var control = (Control)sender;
				var command = (ICommand)control.GetValue(CommandProperty);
				var commandParameter = control.GetValue(CommandParameterProperty);
				command.Execute(commandParameter);
			}
		}
    }
}