﻿using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace yae.Async
{
    public abstract class AsyncOperation<TState, TResult> //: IAsyncOperation<TState, ValueTask<TResult>>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract ValueTask<TResult> CanExecuteSynchronous(TState input);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void Continuation(TState input);

        /// <summary>
        /// Executes the Task with the given input.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public ValueTask<TResult> ExecuteAsync(TState input)
        {
            async ValueTask<TResult> AwaitTask(ValueTask<TResult> t)
            {
                try
                {
                    return await t.ConfigureAwait(false);
                }
                finally
                {
                    Continuation(input);
                }
            }

            var continuation = true;
            try
            {
                var task = CanExecuteSynchronous(input);
                if (task.IsCompletedSuccessfully) return task;
                continuation = false;
                return AwaitTask(task);
            }
            finally
            {
                if (continuation) Continuation(input);
            }
        }

        /// <summary>
        /// <see cref="ValueTask{TResult}"/> merge with <see cref="ValueTask"/>
        /// </summary>
        /// <typeparam name="TIn"></typeparam>
        /// <param name="input"></param>
        /// <param name="operation"></param>
        /// <param name="input2"></param>
        /// <returns></returns>
        public ValueTask MergeWith<TIn>(TState input, VoidAsyncOperation<TIn> operation, TIn input2)
        {
            async ValueTask AwaitMerge(ValueTask t)
            {
                try
                {
                    await t.ConfigureAwait(false);
                }
                finally
                {
                    Continuation(input);
                }
            }
            async ValueTask AwaitBoth(ValueTask<TResult> t1, ValueTask t2)
            {
                try
                {
                    await t1.ConfigureAwait(false);
                    await t2.ConfigureAwait(false);
                }
                finally
                {
                    Continuation(input);
                }
            }

            var release = true;
            try
            {
                var operationTask = CanExecuteSynchronous(input);

                if (!operationTask.IsCompletedSuccessfully)
                {
                    release = false;
                    return AwaitBoth(operationTask, operation.ExecuteAsync(input2));
                }

                var task = operation.ExecuteAsync(input2);
                if (task.IsCompletedSuccessfully) return default;
                release = false;
                return AwaitMerge(task);
            }
            finally
            {
                if (release) Continuation(input);
            }
        }

        /// <summary>
        /// <see cref="ValueTask{TResult}"/> merge with <see cref="ValueTask{TMergeResult}"/>
        /// </summary>
        /// <typeparam name="TIn"></typeparam>
        /// <typeparam name="TMergeResult"></typeparam>
        /// <param name="input"></param>
        /// <param name="operation"></param>
        /// <param name="input2"></param>
        /// <returns></returns>
        public ValueTask<TMergeResult> MergeWith<TIn, TMergeResult>(TState input, AsyncOperation<TIn, TMergeResult> operation, TIn input2)
        {
            async ValueTask<TMergeResult> AwaitMerge(ValueTask<TMergeResult> t)
            {
                try
                {
                    return await t.ConfigureAwait(false);
                }
                finally
                {
                    Continuation(input);
                }
            }
            async ValueTask<TMergeResult> AwaitBoth(ValueTask<TResult> t1, ValueTask<TMergeResult> t2)
            {
                try
                {
                    await t1.ConfigureAwait(false);
                    return await t2.ConfigureAwait(false);
                }
                finally
                {
                    Continuation(input);
                }
            }

            var release = true;
            try
            {
                var operationTask = CanExecuteSynchronous(input);

                if (!operationTask.IsCompletedSuccessfully)
                {
                    release = false;
                    return AwaitBoth(operationTask, operation.ExecuteAsync(input2));
                }

                var task = operation.ExecuteAsync(input2);
                if (task.IsCompletedSuccessfully) return default;
                release = false;
                return AwaitMerge(task);
            }
            finally
            {
                if (release) Continuation(input);
            }
        }
    }
}