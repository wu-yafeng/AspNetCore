// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.AspNetCore.Components.Test.Helpers
{
    public class TestRenderer : Renderer
    {
        public TestRenderer() : this(new TestServiceProvider())
        {
        }

        public TestRenderer(IDispatcher dispatcher) : base(new TestServiceProvider(), NullLoggerFactory.Instance, dispatcher)
        {
        }

        public TestRenderer(IServiceProvider serviceProvider) : base(serviceProvider, NullLoggerFactory.Instance, new RendererSynchronizationContext())
        {
        }

        public Action OnExceptionHandled { get; set; }

        public Action<RenderBatch> OnUpdateDisplay { get; set; }

        public Action OnUpdateDisplayComplete { get; set; }

        public List<CapturedBatch> Batches { get; }
            = new List<CapturedBatch>();

        public List<Exception> HandledExceptions { get; } = new List<Exception>();

        public bool ShouldHandleExceptions { get; set; }

        public Task NextRenderResultTask { get; set; } = Task.CompletedTask;

        public new int AssignRootComponentId(IComponent component)
            => base.AssignRootComponentId(component);

        public void RenderRootComponent(int componentId, ParameterCollection? parameters = default)
        {
            var task = InvokeAsync(() => base.RenderRootComponentAsync(componentId, parameters ?? ParameterCollection.Empty));
            UnwrapTask(task);
        }

        public new Task RenderRootComponentAsync(int componentId)
            => InvokeAsync(() => base.RenderRootComponentAsync(componentId));

        public new Task RenderRootComponentAsync(int componentId, ParameterCollection parameters)
            => InvokeAsync(() => base.RenderRootComponentAsync(componentId, parameters));

        public Task DispatchEventAsync(int eventHandlerId, UIEventArgs args)
            => InvokeAsync(() => base.DispatchEventAsync(eventHandlerId, null, args));

        public new Task DispatchEventAsync(int eventHandlerId, EventFieldInfo eventFieldInfo, UIEventArgs args)
            => InvokeAsync(() => base.DispatchEventAsync(eventHandlerId, eventFieldInfo, args));

        private static Task UnwrapTask(Task task)
        {
            // This should always be run synchronously
            Assert.True(task.IsCompleted);
            if (task.IsFaulted)
            {
                var exception = task.Exception.Flatten().InnerException;
                while (exception is AggregateException e)
                {
                    exception = e.InnerException;
                }

                ExceptionDispatchInfo.Capture(exception).Throw();
            }

            return task;
        }

        public T InstantiateComponent<T>() where T : IComponent
            => (T)InstantiateComponent(typeof(T));

        protected override void HandleException(Exception exception)
        {
            if (!ShouldHandleExceptions)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }

            HandledExceptions.Add(exception);
            OnExceptionHandled?.Invoke();
        }

        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
        {
            OnUpdateDisplay?.Invoke(renderBatch);

            var capturedBatch = new CapturedBatch();
            Batches.Add(capturedBatch);

            for (var i = 0; i < renderBatch.UpdatedComponents.Count; i++)
            {
                ref var renderTreeDiff = ref renderBatch.UpdatedComponents.Array[i];
                capturedBatch.AddDiff(renderTreeDiff);
            }

            // Clone other data, as underlying storage will get reused by later batches
            capturedBatch.ReferenceFrames = renderBatch.ReferenceFrames.AsEnumerable().ToArray();
            capturedBatch.DisposedComponentIDs = renderBatch.DisposedComponentIDs.AsEnumerable().ToList();

            // This renderer updates the UI synchronously, like the WebAssembly one.
            // To test async UI updates, subclass TestRenderer and override UpdateDisplayAsync.

            OnUpdateDisplayComplete?.Invoke();
            return NextRenderResultTask;
        }
    }
}
