using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace Workflow.Studio.Nodify;

public static class WorkflowWorkbenchConnectorInteractions
{
    private static bool _isRegistered;

    public static void Register()
    {
        if (_isRegistered)
        {
            return;
        }

        global::Nodify.Interactivity.InputProcessor.Shared<global::Nodify.Connector>
            .ReplaceHandlerFactory<global::Nodify.Interactivity.ConnectorState.Connecting>(connector => new CustomConnecting(connector));
        global::Nodify.Interactivity.InputProcessor.Shared<global::Nodify.Connector>
            .RegisterHandlerFactory(connector => new RetargetConnections(connector));

        _isRegistered = true;
    }

    private sealed class CustomConnecting : global::Nodify.Interactivity.ConnectorState.Connecting
    {
        protected override bool CanBegin => !RetargetConnections.InProgress;

        public CustomConnecting(global::Nodify.Connector connector)
            : base(connector)
        {
        }
    }

    private sealed class RetargetConnections : global::Nodify.Interactivity.DragState<global::Nodify.Connector>
    {
        public static InputGesture Reconnect { get; } = new global::Nodify.Interactivity.MouseGesture(MouseAction.LeftClick, ModifierKeys.Control)
        {
            IgnoreModifierKeysOnRelease = true
        };

        public static bool InProgress { get; private set; }

        protected override bool CanBegin => IsInputPort(Element.DataContext) && GetBooleanProperty(Element.DataContext, "IsConnected");

        protected override InputGesture DragGesture => Reconnect;

        protected override InputGesture? CancelGesture => Element.ActualGestures.Connector.CancelAction;

        private object? ViewModel => Element.DataContext;

        private global::Nodify.Connector? _targetConnector;

        public RetargetConnections(global::Nodify.Connector element)
            : base(element)
        {
            PositionElement = Element.Editor ?? (IInputElement)Element;
        }

        protected override void OnBegin(InputEventArgs e)
        {
            InProgress = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var position = Element.Editor?.MouseLocation ?? default;
            var connector = Element.FindTargetConnector(position);
            connector?.UpdateAnchor();

            SetTargetConnector(connector);
            UpdateConnections(connector?.Anchor ?? position);
        }

        protected override void OnEnd(InputEventArgs e)
        {
            var position = Element.Editor?.MouseLocation ?? default;
            var connector = Element.FindTargetConnector(position);
            connector?.UpdateAnchor();

            if (connector?.DataContext is { } targetViewModel
                && ViewModel is { } sourceViewModel
                && !ReferenceEquals(sourceViewModel, targetViewModel))
            {
                InvokeRewireConnection(Element.Editor?.DataContext, sourceViewModel, targetViewModel);
            }

            ResetState();
        }

        protected override void OnCancel(InputEventArgs e)
        {
            ResetState();
        }

        private void UpdateConnections(Point position)
        {
            if (ViewModel is null)
            {
                return;
            }

            foreach (var connection in GetEnumerableProperty(ViewModel, "Connections"))
            {
                var target = GetPropertyValue(connection, "Target");
                if (ReferenceEquals(target, ViewModel))
                {
                    SetPropertyValue(target, "Anchor", position);
                }
            }
        }

        private void ResetState()
        {
            SetTargetConnector(null);
            Element.UpdateAnchor();
            InProgress = false;
        }

        private void SetTargetConnector(global::Nodify.Connector? target)
        {
            if (ReferenceEquals(_targetConnector, target))
            {
                return;
            }

            if (_targetConnector is not null)
            {
                global::Nodify.PendingConnection.SetIsOverElement(_targetConnector, false);
            }

            if (target is not null)
            {
                global::Nodify.PendingConnection.SetIsOverElement(target, true);
            }

            _targetConnector = target;
        }

        private static bool IsInputPort(object? dataContext)
        {
            var direction = GetPropertyValue(dataContext, "Direction");
            return string.Equals(direction?.ToString(), "Input", StringComparison.Ordinal);
        }

        private static bool GetBooleanProperty(object? target, string propertyName)
        {
            return GetPropertyValue(target, propertyName) is bool value && value;
        }

        private static IEnumerable GetEnumerableProperty(object target, string propertyName)
        {
            return GetPropertyValue(target, propertyName) as IEnumerable ?? Array.Empty<object>();
        }

        private static object? GetPropertyValue(object? target, string propertyName)
        {
            if (target is null)
            {
                return null;
            }

            return target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(target);
        }

        private static void SetPropertyValue(object? target, string propertyName, object value)
        {
            if (target is null)
            {
                return;
            }

            target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)?.SetValue(target, value);
        }

        private static void InvokeRewireConnection(object? graph, object source, object target)
        {
            graph?.GetType()
                .GetMethod("RewireConnection", BindingFlags.Instance | BindingFlags.Public)
                ?.Invoke(graph, [source, target]);
        }
    }
}
