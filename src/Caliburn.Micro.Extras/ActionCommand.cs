﻿namespace Caliburn.Micro.Extras {
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Windows.Input;
    using Weakly;

    /// <summary>
    /// Wraps a ViewModel method (with guard) in an <see cref="ICommand"/>.
    /// </summary>
    public class ActionCommand : ICommand {
        readonly ActionExecutionContext context;
        readonly WeakEventSource canExecuteChangedSource = new WeakEventSource();
        readonly string guardName;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionCommand"/> class.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="methodName">Name of the method.</param>
        public ActionCommand(object target, string methodName) {
            if (target == null)
                throw new ArgumentNullException("target");

            var method = target.GetType().GetMethod(methodName);
            if (method == null)
                throw new ArgumentException(@"Specified method cannot be found.", "methodName");

            guardName = "Can" + method.Name;
            context = new ActionExecutionContext {
                Target = target,
                Method = method,
            };

            var guard = target.GetType().GetMethod("get_" + guardName);
            var inpc = target as INotifyPropertyChanged;
            if (inpc == null || guard == null) return;

            WeakEventHandler.Register<PropertyChangedEventArgs>(inpc, "PropertyChanged", OnPropertyChanged);
            context.CanExecute = new WeakFunc<bool>(inpc, guard).Invoke;
        }

        void OnPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == guardName) {
                Micro.Execute.OnUIThread(() => canExecuteChangedSource.Raise(this, EventArgs.Empty));
            }
        }

        /// <summary>
        /// Defines the method to be called when the command is invoked.
        /// </summary>
        /// <param name="parameter">Data used by the command. If the command does not require data to be passed, this object can be set to null.</param>
        public void Execute(object parameter) {
            var method = DynamicDelegate.From(context.Method);
            var returnValue = method(context.Target, new object[0]);

            var task = returnValue as System.Threading.Tasks.Task;
            if (task != null) {
                returnValue = task.AsResult();
            }

            var result = returnValue as IResult;
            if (result != null) {
                returnValue = new[] { result };
            }

            var enumerable = returnValue as IEnumerable<IResult>;
            if (enumerable != null) {
                returnValue = enumerable.GetEnumerator();
            }

            var enumerator = returnValue as IEnumerator<IResult>;
            if (enumerator != null) {
                Coroutine.BeginExecute(enumerator, context);
            }
        }

        /// <summary>
        /// Defines the method that determines whether the command can execute in its current state.
        /// </summary>
        /// <param name="parameter">Data used by the command. If the command does not require data to be passed, this object can be set to null.</param>
        /// <returns>true if this command can be executed; otherwise, false.</returns>
        public bool CanExecute(object parameter) {
            return context.CanExecute == null || context.CanExecute();
        }

        /// <summary>
        /// Occurs when changes occur that affect whether the command should execute.
        /// </summary>
        public event EventHandler CanExecuteChanged {
            add { canExecuteChangedSource.Add(value); }
            remove { canExecuteChangedSource.Remove(value); }
        }
    }
}
