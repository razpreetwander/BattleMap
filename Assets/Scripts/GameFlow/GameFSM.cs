using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Libraries.GameFlow.FSM
{
    /// <summary>
    /// Reconstructed from GameFlow.dll (IL2CPP metadata v39, index 23130).
    /// </summary>
    public interface IStateProperties
    {
        string StateId { get; }
        bool AllowDuplicates { get; }
    }

    /// <summary>
    /// Reconstructed from GameFlow.dll (index 23136).
    /// </summary>
    public interface IGameStateFactory
    {
        TState Create<TState>() where TState : FSMStateBase, new();
    }

    /// <summary>
    /// Reconstructed from GameFlow.dll (index 23135).
    /// </summary>
    public class CustomGameStateFactory : IGameStateFactory
    {
        private readonly Dictionary<Type, Func<FSMStateBase>> _creators = new();

        public void Register<TState>(Func<TState> creator) where TState : FSMStateBase
        {
            _creators[typeof(TState)] = () => creator();
        }

        public TState Create<TState>() where TState : FSMStateBase, new()
        {
            if (_creators.TryGetValue(typeof(TState), out var factory))
                return (TState)factory();
            return new TState();
        }
    }

    /// <summary>
    /// Base state matching Libraries.GameFlow.FSM.FSMStateBase (index 23129, 11 methods).
    /// </summary>
    public abstract class FSMStateBase : IStateProperties
    {
        public virtual string StateId => GetType().Name;
        public virtual bool AllowDuplicates => false;
        public bool IsActive { get; private set; }
        public bool IsPaused { get; private set; }
        protected GameFSM OwnerFsm { get; private set; }

        internal void Attach(GameFSM fsm) => OwnerFsm = fsm;

        public virtual Task OnEnterAsync()
        {
            IsActive = true;
            IsPaused = false;
            return Task.CompletedTask;
        }

        public virtual Task OnExitAsync()
        {
            IsActive = false;
            IsPaused = false;
            return Task.CompletedTask;
        }

        public virtual void OnPause() => IsPaused = true;
        public virtual void OnResume() => IsPaused = false;
        public virtual void OnUpdate(float deltaTime) { }
    }

    public abstract class FSMState : FSMStateBase { }

    public abstract class FSMState<TContext> : FSMStateBase
    {
        public TContext Context { get; private set; }

        public void SetContext(TContext context) => Context = context;
    }

    /// <summary>
    /// Reconstructed command types from GameFlow.dll (indices 23120..23126).
    /// </summary>
    public abstract class FSMCommand
    {
        public abstract Task ExecuteAsync(GameFSM fsm);
    }

    public sealed class PushStateCommand : FSMCommand
    {
        private readonly FSMStateBase _state;
        public PushStateCommand(FSMStateBase state) => _state = state;
        public override Task ExecuteAsync(GameFSM fsm) => fsm.InternalPushAsync(_state);
    }

    public sealed class PopStateCommand : FSMCommand
    {
        public override Task ExecuteAsync(GameFSM fsm) => fsm.InternalPopAsync();
    }

    public sealed class ClearStackCommand : FSMCommand
    {
        public override Task ExecuteAsync(GameFSM fsm) => fsm.InternalClearStackAsync();
    }

    /// <summary>
    /// Reconstructed from Libraries.GameFlow.FSM.GameFSM (index 23134, 9 methods).
    /// Manages async state stack transitions (Home, Gameplay, LevelEnd, World, Chapters, Party).
    /// </summary>
    public class GameFSM
    {
        private readonly Stack<FSMStateBase> _stateStack = new();
        private readonly Queue<FSMCommand> _commandQueue = new();
        private readonly IGameStateFactory _factory;
        private bool _isProcessing;

        public FSMStateBase CurrentState => _stateStack.Count > 0 ? _stateStack.Peek() : null;
        public int StackDepth => _stateStack.Count;
        public event Action<FSMStateBase, FSMStateBase> StateChanged;

        public GameFSM(IGameStateFactory factory = null)
        {
            _factory = factory ?? new CustomGameStateFactory();
        }

        public void Enqueue(FSMCommand command)
        {
            if (command == null) return;
            _commandQueue.Enqueue(command);
            if (!_isProcessing) _ = RunAsync();
        }

        public async Task SwitchToStateAsync<TState>() where TState : FSMStateBase, new()
        {
            var next = _factory.Create<TState>();
            await SwitchToStateAsync(next);
        }

        public async Task SwitchToStateAsync(FSMStateBase nextState)
        {
            if (nextState == null) return;
            _commandQueue.Enqueue(new ClearStackCommand());
            _commandQueue.Enqueue(new PushStateCommand(nextState));
            await RunAsync();
        }

        public Task PushStateAsync(FSMStateBase state)
        {
            _commandQueue.Enqueue(new PushStateCommand(state));
            return RunAsync();
        }

        public Task PopStateAsync()
        {
            _commandQueue.Enqueue(new PopStateCommand());
            return RunAsync();
        }

        public void Tick(float deltaTime)
        {
            var top = CurrentState;
            if (top != null && top.IsActive && !top.IsPaused)
                top.OnUpdate(deltaTime);
        }

        private async Task RunAsync()
        {
            if (_isProcessing) return;
            _isProcessing = true;
            try
            {
                while (_commandQueue.Count > 0)
                {
                    var cmd = _commandQueue.Dequeue();
                    await cmd.ExecuteAsync(this);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                _isProcessing = false;
            }
        }

        internal async Task InternalPushAsync(FSMStateBase state)
        {
            var prev = CurrentState;
            if (prev != null) prev.OnPause();
            state.Attach(this);
            _stateStack.Push(state);
            await state.OnEnterAsync();
            StateChanged?.Invoke(prev, state);
        }

        internal async Task InternalPopAsync()
        {
            if (_stateStack.Count == 0) return;
            var prev = _stateStack.Pop();
            await prev.OnExitAsync();
            var next = CurrentState;
            next?.OnResume();
            StateChanged?.Invoke(prev, next);
        }

        internal async Task InternalClearStackAsync()
        {
            var prev = CurrentState;
            while (_stateStack.Count > 0)
            {
                var top = _stateStack.Pop();
                await top.OnExitAsync();
            }
            if (prev != null) StateChanged?.Invoke(prev, null);
        }
    }
}
