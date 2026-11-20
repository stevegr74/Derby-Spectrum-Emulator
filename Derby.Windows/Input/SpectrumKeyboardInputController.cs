using System.Windows.Forms;

namespace Derby
{
    public sealed class SpectrumKeyboardInputController
    {
        private readonly SpectrumKeyInputBridge bridge = new();
        private readonly SpectrumKeyInputBridge<OnScreenKeyStroke> onScreenBridge = new();
        private readonly HashSet<Keys> logicalKeysDown = new();
        private readonly HashSet<OnScreenKeyStroke> onScreenKeysDown = new();
        private readonly bool[,] appliedMatrix = new bool[8, 5];

        public bool HandleHostKeyEvent(
            Keys key,
            bool isDown,
            Func<int, ulong> getRowScanCount,
            ulong currentTStates,
            ulong maximumDeferredReleaseTStates,
            Action<int, int, bool> setMatrixKey)
        {
            if (!SpectrumKeyboardMapping.IsMappedKey(key))
                return false;

            IReadOnlyList<SpectrumKeyInputBridge.SpectrumKeyStateChange> changes = isDown
                ? bridge.RegisterKeyDown(key, SpectrumKeyboardMapping.GetRows(key), getRowScanCount, currentTStates)
                : bridge.RegisterKeyUp(key, getRowScanCount, currentTStates, maximumDeferredReleaseTStates);

            return ApplyChanges(changes, logicalKeysDown) && ApplyMatrix(setMatrixKey);
        }

        internal void HandleOnScreenKeyEvent(
            OnScreenKeyStroke stroke,
            bool isDown,
            Func<int, ulong> getRowScanCount,
            ulong currentTStates,
            ulong maximumDeferredReleaseTStates,
            Action<int, int, bool> setMatrixKey)
        {
            var changes = isDown
                ? onScreenBridge.RegisterKeyDown(stroke, stroke.Rows, getRowScanCount, currentTStates)
                : onScreenBridge.RegisterKeyUp(stroke, getRowScanCount, currentTStates, maximumDeferredReleaseTStates);
            if (ApplyChanges(changes, onScreenKeysDown))
                ApplyMatrix(setMatrixKey);
        }

        internal void CancelOnScreenKeys(Action<int, int, bool> setMatrixKey)
        {
            onScreenBridge.Reset();
            if (onScreenKeysDown.Count == 0)
                return;
            onScreenKeysDown.Clear();
            ApplyMatrix(setMatrixKey);
        }

        public bool Advance(
            Func<int, ulong> getRowScanCount,
            ulong currentTStates,
            Action<int, int, bool> setMatrixKey)
        {
            bool hostChanged = ApplyChanges(bridge.CollectStateChanges(getRowScanCount, currentTStates), logicalKeysDown);
            bool onScreenChanged = ApplyChanges(
                onScreenBridge.CollectStateChanges(getRowScanCount, currentTStates), onScreenKeysDown);
            return (hostChanged || onScreenChanged) && ApplyMatrix(setMatrixKey);
        }

        public void Reset()
        {
            bridge.Reset();
            onScreenBridge.Reset();
            logicalKeysDown.Clear();
            onScreenKeysDown.Clear();
            Array.Clear(appliedMatrix);
        }

        public string DescribeKeyState(Keys key, Func<int, ulong> getRowScanCount) =>
            bridge.DescribeKeyState(key, getRowScanCount);

        private static bool ApplyChanges<TKey>(
            IReadOnlyList<SpectrumKeyInputBridge<TKey>.SpectrumKeyStateChange> changes,
            HashSet<TKey> keysDown) where TKey : notnull
        {
            if (changes.Count == 0)
                return false;

            foreach (var change in changes)
            {
                if (change.Pressed)
                    keysDown.Add(change.Key);
                else
                    keysDown.Remove(change.Key);
            }
            return true;
        }

        private bool ApplyMatrix(Action<int, int, bool> setMatrixKey)
        {
            bool[,] desiredMatrix = new bool[8, 5];
            foreach (Keys key in logicalKeysDown)
            {
                foreach (SpectrumKeyboardMapping.MatrixPosition position in SpectrumKeyboardMapping.GetPositions(key))
                    desiredMatrix[position.Row, position.Bit] = true;
            }
            foreach (OnScreenKeyStroke stroke in onScreenKeysDown)
            {
                foreach (SpectrumKeyboardMapping.MatrixPosition position in stroke.Positions)
                    desiredMatrix[position.Row, position.Bit] = true;
            }

            for (int row = 0; row < 8; row++)
            {
                for (int bit = 0; bit < 5; bit++)
                {
                    if (desiredMatrix[row, bit] == appliedMatrix[row, bit])
                        continue;

                    appliedMatrix[row, bit] = desiredMatrix[row, bit];
                    setMatrixKey(row, bit, appliedMatrix[row, bit]);
                }
            }

            return true;
        }
    }
}
