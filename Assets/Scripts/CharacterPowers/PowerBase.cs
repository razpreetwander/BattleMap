using System;
using BattleMaps.Gameplay;
using BattleMaps.Signals;
using UnityEngine;

namespace BattleMaps.CharacterPowers.Core
{
    using BattleMaps.CharacterPowers.Powers;

    /// <summary>
    /// Reconstructed from BattleMaps.CharacterPowers.Core.PowerBase (IL2CPP index 4926, 27 methods).
    /// </summary>
    public abstract class PowerBase : IDisposable
    {
        public PowerType Type { get; protected set; }
        public bool IsActive { get; protected set; }
        public bool IsPaused { get; protected set; }
        public float RemainingDuration { get; protected set; }
        public float TotalDuration { get; protected set; }

        protected PlayerBase Owner { get; private set; }
        protected GameplayController Gameplay { get; private set; }
        protected ZoneManager ZoneManager => Gameplay != null ? Gameplay.ZoneManager : null;
        protected PowerSettingsBase Settings { get; private set; }

        public event Action<PowerBase> PowerActivated;
        public event Action<PowerBase> PowerPaused;
        public event Action<PowerBase, float> PowerUpdated;

        public void Initialize(PlayerBase owner, GameplayController gameplay, PowerSettingsBase settings)
        {
            Owner = owner;
            Gameplay = gameplay;
            Settings = settings ?? new PowerSettingsBase { Type = Type };
            Type = Settings.Type;
            TotalDuration = Mathf.Max(0.25f, Settings.Duration);
            IsActive = false;
            IsPaused = false;
            OnPowerInitialized();
        }

        public void Activate()
        {
            if (Owner == null || !Owner.IsAlive) return;
            IsActive = true;
            IsPaused = false;
            RemainingDuration = TotalDuration;
            OnPowerActivated();
            PowerActivated?.Invoke(this);
            EventBus.Shared.Publish(new PowerActivatedSignal(Owner.PlayerId, Type.ToString(), TotalDuration));
        }

        public void Pause(bool paused)
        {
            IsPaused = paused;
            if (paused)
            {
                OnPowerPaused();
                PowerPaused?.Invoke(this);
            }
        }

        public void Update(float deltaTime)
        {
            if (!IsActive || IsPaused) return;
            if (Owner == null || !Owner.IsAlive)
            {
                Deactivate();
                return;
            }

            RemainingDuration -= deltaTime;
            OnPowerUpdated(deltaTime);
            PowerUpdated?.Invoke(this, Mathf.Clamp01(RemainingDuration / Mathf.Max(0.01f, TotalDuration)));

            if (RemainingDuration <= 0f)
                Deactivate();
        }

        public void Deactivate()
        {
            if (!IsActive) return;
            IsActive = false;
            OnPowerDeactivated();
        }

        public virtual void OnChargeComplete() { }
        protected virtual void OnPowerInitialized() { }
        protected virtual void OnPowerActivated() { }
        protected virtual void OnPowerPaused() { }
        protected virtual void OnPowerUpdated(float dt) { }
        protected virtual void OnPowerDeactivated() { }
        protected virtual void OnPowerDestroyed() { }

        public void Dispose()
        {
            Deactivate();
            OnPowerDestroyed();
        }
    }
}
