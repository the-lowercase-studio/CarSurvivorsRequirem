using System;
using Assets.Scripts.Common.EventArgs;
using Assets.Scripts.GameFlow;
using Reflex.Attributes;
using UnityEngine;

namespace Assets.Scripts.LevelSystem
{
    public readonly struct LevelData
    {
        public readonly byte Lvl;
        public readonly float Exp;
        public readonly float MaxExp;

        public LevelData(byte lvl = 1, float exp = 0, float maxExp = float.MaxValue)
        {
            Lvl = lvl;
            Exp = exp;
            MaxExp = maxExp;
        }

        public void Deconstruct(out byte lvl, out float exp, out float maxExp)
        {
            lvl = Lvl;
            exp = Exp;
            maxExp = MaxExp;
        }

        public override string ToString()
        {
            return $"LevelData(DangerLevel: {Lvl}, Exp: {Exp}, MaxExp: {MaxExp})";
        }
    }

    public interface ILevelController
    {
        public LevelData LevelData { get; }

        public event EventHandler<ValueEventArgs<LevelData>> OnExpChange;

        public event EventHandler<ValueEventArgs<LevelData>> OnLvlUp;

        public void AddExp(float value);
    }

    public class LevelController : MonoBehaviour, ILevelController
    {
        [Inject] private readonly IRunSessionService _runSessionService = null;

        [SerializeField] private AnimationCurve _expCurve;

        public LevelData LevelData { get; private set; } = new LevelData();

        public event EventHandler<ValueEventArgs<LevelData>> OnLvlUp;

        public event EventHandler<ValueEventArgs<LevelData>> OnExpChange;

        private void Awake()
        {
            if (_runSessionService != null && _runSessionService.TryGetRestoredLevel(out byte restoredLevel, out float restoredExp))
            {
                byte safeLevel = restoredLevel < 1 ? (byte)1 : restoredLevel;
                float safeExp = Mathf.Max(0f, restoredExp);
                LevelData = new LevelData(safeLevel, safeExp, CalculateMaxExp(safeLevel));
                return;
            }

            LevelData = new LevelData(maxExp: CalculateMaxExp(LevelData.Lvl));
        }

        public void AddExp(float value)
        {
            if (LevelData.Lvl == byte.MaxValue)
            {
                return;
            }

            (byte lvl, float exp, float maxExp) = LevelData;
            byte prevLvl = lvl;
            exp += value;

            while (lvl < byte.MaxValue && exp >= maxExp)
            {
                lvl++;
                exp -= maxExp;
                maxExp = CalculateMaxExp(lvl);

                LevelData = new LevelData(lvl, exp, maxExp);
                OnLvlUp?.Invoke(this, new(LevelData));
            }

            LevelData = new LevelData(lvl, exp, maxExp);
            OnExpChange?.Invoke(this, new(LevelData));
        }

        private float CalculateMaxExp(byte level)
        {
            return _expCurve.Evaluate(level);
        }
    }
}

