﻿//-----------------------------------------------------------------------------
// FILE:	    LockProvider.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Tasks;

using k8s;
using k8s.Models;

namespace Neon.Kube.Operator.ResourceManager
{
    internal class LockProvider<TEntity> : ILockProvider<TEntity>
        where TEntity : IKubernetesObject<V1ObjectMeta>, new()
    {
        private static readonly ObjectPool<SemaphoreSlim> semaphorePool = new DefaultObjectPool<SemaphoreSlim>(new SemaphoreSlimPooledObjectPolicy(), 20);
        private bool isDisposed;

        static readonly ConcurrentDictionary<string, SemaphoreSlim> lockDictionary = new ConcurrentDictionary<string, SemaphoreSlim>();

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            if (isDisposed)
                return ValueTask.CompletedTask;

            isDisposed = true;

            return ValueTask.CompletedTask;
        }

        /// <inheritdoc/>
        public void Release(string entityId)
        {
            SemaphoreSlim semaphore;

            if (lockDictionary.TryGetValue(entityId, out semaphore))
            {
                semaphore.Release();
                if (semaphore.CurrentCount > 0)
                {
                    semaphorePool.Return(semaphore);
                    lockDictionary.TryRemove(entityId, out _);
                }
            }
        }

        /// <inheritdoc/>
        public async Task<IAsyncDisposable> WaitAsync(string entityId)
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(LockProvider<TEntity>));


            var semaphore = lockDictionary.GetOrAdd(entityId, semaphorePool.Get());

            await semaphore.WaitAsync();

            return new SafeSemaphoreRelease(entityId, this);
        }

        private struct SafeSemaphoreRelease : IAsyncDisposable, IDisposable
        {
            private string entityId;
            private LockProvider<TEntity> lockProvider;

            public SafeSemaphoreRelease(string entityId, LockProvider<TEntity> lockProvider)
            {
                this.entityId = entityId;
                this.lockProvider = lockProvider;
            }

            public ValueTask DisposeAsync()
            {
                lockProvider.Release(entityId);

                return ValueTask.CompletedTask;
            }

            public void Dispose()
            {
                lockProvider.Release(entityId);
            }
        }

        private class SemaphoreSlimPooledObjectPolicy : PooledObjectPolicy<SemaphoreSlim>
        {
            /// <inheritdoc/>
            public override SemaphoreSlim Create()
            {
                return new SemaphoreSlim(1, 1);
            }

            /// <inheritdoc/>
            public override bool Return(SemaphoreSlim semaphore)
            {
                // Don't return items to the pool that are locked.
                return semaphore.CurrentCount == 1;
            }
        }
    }
}
