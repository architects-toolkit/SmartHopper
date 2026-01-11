/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 * 
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Core.ComponentBase;
using Xunit;

namespace SmartHopper.Core.Tests.ComponentBase
{
    /// <summary>
    /// Unit tests for the ComponentStateManager class.
    /// Tests state transitions, debouncing, hash management, and file restoration scenarios.
    /// </summary>
    public class ComponentStateManagerTests : IDisposable
    {
        private readonly ComponentStateManager manager;

        public ComponentStateManagerTests()
        {
            this.manager = new ComponentStateManager("TestComponent");
        }

        public void Dispose()
        {
            this.manager?.Dispose();
        }

        #region Initial State Tests

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: Initial state is Completed  [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: Initial state is Completed  [Core]")]
#endif
        public void InitialState_IsCompleted()
        {
            Assert.Equal(ComponentState.Completed, this.manager.CurrentState);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: Initial state is not transitioning [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: Initial state is not transitioning [Core]")]
#endif
        public void InitialState_IsNotTransitioning()
        {
            Assert.False(this.manager.IsTransitioning);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: Initial state is not restoring [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: Initial state is not restoring [Core]")]
#endif
        public void InitialState_IsNotRestoring()
        {
            Assert.False(this.manager.IsRestoringFromFile);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: Initial state has no pending transitions [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: Initial state has no pending transitions [Core]")]
#endif
        public void InitialState_NoPendingTransitions()
        {
            Assert.Equal(0, this.manager.PendingTransitionCount);
        }

        #endregion

        #region Valid Transition Tests

#if NET7_WINDOWS
        [Theory(DisplayName = "ComponentStateManager: Valid transitions from Completed [Windows]")]
#else
        [Theory(DisplayName = "ComponentStateManager: Valid transitions from Completed [Core]")]
#endif
        [InlineData(ComponentState.Waiting)]
        [InlineData(ComponentState.NeedsRun)]
        [InlineData(ComponentState.Processing)]
        [InlineData(ComponentState.Error)]
        public void ValidTransitions_FromCompleted(ComponentState targetState)
        {
            Assert.True(this.manager.IsValidTransition(ComponentState.Completed, targetState));
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "ComponentStateManager: Valid transitions from Waiting [Windows]")]
#else
        [Theory(DisplayName = "ComponentStateManager: Valid transitions from Waiting [Core]")]
#endif
        [InlineData(ComponentState.NeedsRun)]
        [InlineData(ComponentState.Processing)]
        [InlineData(ComponentState.Error)]
        public void ValidTransitions_FromWaiting(ComponentState targetState)
        {
            Assert.True(this.manager.IsValidTransition(ComponentState.Waiting, targetState));
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "ComponentStateManager: Valid transitions from NeedsRun [Windows]")]
#else
        [Theory(DisplayName = "ComponentStateManager: Valid transitions from NeedsRun [Core]")]
#endif
        [InlineData(ComponentState.Processing)]
        [InlineData(ComponentState.Error)]
        public void ValidTransitions_FromNeedsRun(ComponentState targetState)
        {
            Assert.True(this.manager.IsValidTransition(ComponentState.NeedsRun, targetState));
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "ComponentStateManager: Valid transitions from Processing [Windows]")]
#else
        [Theory(DisplayName = "ComponentStateManager: Valid transitions from Processing [Core]")]
#endif
        [InlineData(ComponentState.Completed)]
        [InlineData(ComponentState.Cancelled)]
        [InlineData(ComponentState.Error)]
        public void ValidTransitions_FromProcessing(ComponentState targetState)
        {
            Assert.True(this.manager.IsValidTransition(ComponentState.Processing, targetState));
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "ComponentStateManager: Valid transitions from Cancelled [Windows]")]
#else
        [Theory(DisplayName = "ComponentStateManager: Valid transitions from Cancelled [Core]")]
#endif
        [InlineData(ComponentState.Waiting)]
        [InlineData(ComponentState.NeedsRun)]
        [InlineData(ComponentState.Processing)]
        [InlineData(ComponentState.Error)]
        public void ValidTransitions_FromCancelled(ComponentState targetState)
        {
            Assert.True(this.manager.IsValidTransition(ComponentState.Cancelled, targetState));
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "ComponentStateManager: Valid transitions from Error [Windows]")]
#else
        [Theory(DisplayName = "ComponentStateManager: Valid transitions from Error [Core]")]
#endif
        [InlineData(ComponentState.Waiting)]
        [InlineData(ComponentState.NeedsRun)]
        [InlineData(ComponentState.Processing)]
        public void ValidTransitions_FromError(ComponentState targetState)
        {
            Assert.True(this.manager.IsValidTransition(ComponentState.Error, targetState));
        }

        #endregion

        #region Invalid Transition Tests

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: Same state transition is invalid [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: Same state transition is invalid [Core]")]
#endif
        public void InvalidTransition_SameState()
        {
            Assert.False(this.manager.IsValidTransition(ComponentState.Completed, ComponentState.Completed));
            Assert.False(this.manager.IsValidTransition(ComponentState.Processing, ComponentState.Processing));
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "ComponentStateManager: Invalid transitions from Waiting [Windows]")]
#else
        [Theory(DisplayName = "ComponentStateManager: Invalid transitions from Waiting [Core]")]
#endif
        [InlineData(ComponentState.Completed)]
        [InlineData(ComponentState.Cancelled)]
        public void InvalidTransitions_FromWaiting(ComponentState targetState)
        {
            Assert.False(this.manager.IsValidTransition(ComponentState.Waiting, targetState));
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "ComponentStateManager: Invalid transitions from NeedsRun [Windows]")]
#else
        [Theory(DisplayName = "ComponentStateManager: Invalid transitions from NeedsRun [Core]")]
#endif
        [InlineData(ComponentState.Completed)]
        [InlineData(ComponentState.Waiting)]
        [InlineData(ComponentState.Cancelled)]
        public void InvalidTransitions_FromNeedsRun(ComponentState targetState)
        {
            Assert.False(this.manager.IsValidTransition(ComponentState.NeedsRun, targetState));
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "ComponentStateManager: Invalid transitions from Processing [Windows]")]
#else
        [Theory(DisplayName = "ComponentStateManager: Invalid transitions from Processing [Core]")]
#endif
        [InlineData(ComponentState.Waiting)]
        [InlineData(ComponentState.NeedsRun)]
        public void InvalidTransitions_FromProcessing(ComponentState targetState)
        {
            Assert.False(this.manager.IsValidTransition(ComponentState.Processing, targetState));
        }

        #endregion

        #region RequestTransition Tests

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: RequestTransition changes state [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: RequestTransition changes state [Core]")]
#endif
        public void RequestTransition_ChangesState()
        {
            var result = this.manager.RequestTransition(ComponentState.NeedsRun, TransitionReason.InputChanged);

            Assert.True(result);
            Assert.Equal(ComponentState.NeedsRun, this.manager.CurrentState);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: RequestTransition fires events in order [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: RequestTransition fires events in order [Core]")]
#endif
        public void RequestTransition_FiresEventsInOrder()
        {
            var events = new List<string>();

            this.manager.StateExited += (old) => events.Add($"Exited:{old}");
            this.manager.StateEntered += (newState) => events.Add($"Entered:{newState}");
            this.manager.StateChanged += (old, newState) => events.Add($"Changed:{old}->{newState}");

            this.manager.RequestTransition(ComponentState.NeedsRun, TransitionReason.InputChanged);

            Assert.Equal(3, events.Count);
            Assert.Equal("Exited:Completed", events[0]);
            Assert.Equal("Entered:NeedsRun", events[1]);
            Assert.Equal("Changed:Completed->NeedsRun", events[2]);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: RequestTransition rejects invalid transition [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: RequestTransition rejects invalid transition [Core]")]
#endif
        public void RequestTransition_RejectsInvalidTransition()
        {
            string rejectionMessage = null;
            this.manager.TransitionRejected += (from, to, msg) => rejectionMessage = msg;

            // NeedsRun -> Waiting is invalid
            this.manager.ForceState(ComponentState.NeedsRun);
            var result = this.manager.RequestTransition(ComponentState.Waiting, TransitionReason.InputChanged);

            Assert.False(result);
            Assert.Equal(ComponentState.NeedsRun, this.manager.CurrentState);
            Assert.NotNull(rejectionMessage);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: Sequential transitions execute in order [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: Sequential transitions execute in order [Core]")]
#endif
        public void RequestTransition_SequentialTransitions()
        {
            // Completed -> NeedsRun -> Processing -> Completed
            this.manager.RequestTransition(ComponentState.NeedsRun, TransitionReason.InputChanged);
            Assert.Equal(ComponentState.NeedsRun, this.manager.CurrentState);

            this.manager.RequestTransition(ComponentState.Processing, TransitionReason.RunEnabled);
            Assert.Equal(ComponentState.Processing, this.manager.CurrentState);

            this.manager.RequestTransition(ComponentState.Completed, TransitionReason.ProcessingComplete);
            Assert.Equal(ComponentState.Completed, this.manager.CurrentState);
        }

        #endregion

        #region ForceState Tests

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: ForceState bypasses validation [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: ForceState bypasses validation [Core]")]
#endif
        public void ForceState_BypassesValidation()
        {
            // Force to Processing without going through valid path
            this.manager.ForceState(ComponentState.Processing);
            Assert.Equal(ComponentState.Processing, this.manager.CurrentState);

            // Force back to Completed (valid)
            this.manager.ForceState(ComponentState.Completed);
            Assert.Equal(ComponentState.Completed, this.manager.CurrentState);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: ForceState fires events [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: ForceState fires events [Core]")]
#endif
        public void ForceState_FiresEvents()
        {
            var stateChanged = false;
            this.manager.StateChanged += (old, newState) => stateChanged = true;

            this.manager.ForceState(ComponentState.Processing);

            Assert.True(stateChanged);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: ForceState to same state does nothing [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: ForceState to same state does nothing [Core]")]
#endif
        public void ForceState_SameState_NoEvent()
        {
            var eventCount = 0;
            this.manager.StateChanged += (old, newState) => eventCount++;

            this.manager.ForceState(ComponentState.Completed);

            Assert.Equal(0, eventCount);
        }

        #endregion

        #region Hash Management Tests

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: UpdatePendingHashes stores hashes [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: UpdatePendingHashes stores hashes [Core]")]
#endif
        public void UpdatePendingHashes_StoresHashes()
        {
            var hashes = new Dictionary<string, int>
            {
                { "Input1", 123 },
                { "Input2", 456 },
            };

            this.manager.UpdatePendingHashes(hashes);
            this.manager.CommitHashes();

            var committed = this.manager.GetCommittedHashes();
            Assert.Equal(2, committed.Count);
            Assert.Equal(123, committed["Input1"]);
            Assert.Equal(456, committed["Input2"]);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: GetChangedInputs detects new inputs [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: GetChangedInputs detects new inputs [Core]")]
#endif
        public void GetChangedInputs_DetectsNewInputs()
        {
            // Commit initial state (empty)
            this.manager.CommitHashes();

            // Add new input
            var hashes = new Dictionary<string, int> { { "Input1", 123 } };
            this.manager.UpdatePendingHashes(hashes);

            var changed = this.manager.GetChangedInputs();
            Assert.Single(changed);
            Assert.Equal("Input1", changed[0]);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: GetChangedInputs detects changed values [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: GetChangedInputs detects changed values [Core]")]
#endif
        public void GetChangedInputs_DetectsChangedValues()
        {
            // Commit initial state
            var initial = new Dictionary<string, int> { { "Input1", 123 } };
            this.manager.UpdatePendingHashes(initial);
            this.manager.CommitHashes();

            // Change value
            var updated = new Dictionary<string, int> { { "Input1", 456 } };
            this.manager.UpdatePendingHashes(updated);

            var changed = this.manager.GetChangedInputs();
            Assert.Single(changed);
            Assert.Equal("Input1", changed[0]);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: GetChangedInputs detects removed inputs [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: GetChangedInputs detects removed inputs [Core]")]
#endif
        public void GetChangedInputs_DetectsRemovedInputs()
        {
            // Commit initial state with two inputs
            var initial = new Dictionary<string, int>
            {
                { "Input1", 123 },
                { "Input2", 456 },
            };
            this.manager.UpdatePendingHashes(initial);
            this.manager.CommitHashes();

            // Remove one input
            var updated = new Dictionary<string, int> { { "Input1", 123 } };
            this.manager.UpdatePendingHashes(updated);

            var changed = this.manager.GetChangedInputs();
            Assert.Single(changed);
            Assert.Equal("Input2", changed[0]);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: GetChangedInputs returns empty when unchanged [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: GetChangedInputs returns empty when unchanged [Core]")]
#endif
        public void GetChangedInputs_ReturnsEmpty_WhenUnchanged()
        {
            var hashes = new Dictionary<string, int> { { "Input1", 123 } };
            this.manager.UpdatePendingHashes(hashes);
            this.manager.CommitHashes();

            // Same values
            this.manager.UpdatePendingHashes(hashes);

            var changed = this.manager.GetChangedInputs();
            Assert.Empty(changed);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: ClearHashes removes all tracking [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: ClearHashes removes all tracking [Core]")]
#endif
        public void ClearHashes_RemovesAllTracking()
        {
            var hashes = new Dictionary<string, int> { { "Input1", 123 } };
            this.manager.UpdatePendingHashes(hashes);
            this.manager.CommitHashes();

            this.manager.ClearHashes();

            Assert.Empty(this.manager.GetCommittedHashes());
        }

        #endregion

        #region File Restoration Tests

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: BeginRestoration sets flags [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: BeginRestoration sets flags [Core]")]
#endif
        public void BeginRestoration_SetsFlags()
        {
            this.manager.BeginRestoration();

            Assert.True(this.manager.IsRestoringFromFile);
            Assert.True(this.manager.IsSuppressingInputChanges);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: EndRestoration clears restoring flag but keeps suppression [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: EndRestoration clears restoring flag but keeps suppression [Core]")]
#endif
        public void EndRestoration_ClearsRestoringFlag_KeepsSuppression()
        {
            this.manager.BeginRestoration();
            this.manager.EndRestoration();

            Assert.False(this.manager.IsRestoringFromFile);
            Assert.True(this.manager.IsSuppressingInputChanges);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: ClearSuppressionAfterFirstSolve clears suppression [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: ClearSuppressionAfterFirstSolve clears suppression [Core]")]
#endif
        public void ClearSuppressionAfterFirstSolve_ClearsSuppression()
        {
            this.manager.BeginRestoration();
            this.manager.EndRestoration();
            this.manager.ClearSuppressionAfterFirstSolve();

            Assert.False(this.manager.IsSuppressingInputChanges);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: GetChangedInputs returns empty during suppression [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: GetChangedInputs returns empty during suppression [Core]")]
#endif
        public void GetChangedInputs_ReturnsEmpty_DuringSuppression()
        {
            // Setup: commit initial, then change
            var initial = new Dictionary<string, int> { { "Input1", 123 } };
            this.manager.UpdatePendingHashes(initial);
            this.manager.CommitHashes();

            var updated = new Dictionary<string, int> { { "Input1", 456 } };
            this.manager.UpdatePendingHashes(updated);

            // Begin restoration (suppresses detection)
            this.manager.BeginRestoration();

            var changed = this.manager.GetChangedInputs();
            Assert.Empty(changed);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: RestoreCommittedHashes sets both committed and pending [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: RestoreCommittedHashes sets both committed and pending [Core]")]
#endif
        public void RestoreCommittedHashes_SetsBothCommittedAndPending()
        {
            var hashes = new Dictionary<string, int> { { "Input1", 123 } };
            var branchCounts = new Dictionary<string, int> { { "Input1", 1 } };

            this.manager.RestoreCommittedHashes(hashes, branchCounts);

            var committed = this.manager.GetCommittedHashes();
            Assert.Single(committed);
            Assert.Equal(123, committed["Input1"]);

            // After restoration, no changes should be detected
            var changed = this.manager.GetChangedInputs();
            Assert.Empty(changed);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: Full restoration flow prevents data loss [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: Full restoration flow prevents data loss [Core]")]
#endif
        public void FullRestorationFlow_PreventsDataLoss()
        {
            // Simulate file restoration flow
            this.manager.BeginRestoration();

            // Restore hashes from file
            var restoredHashes = new Dictionary<string, int> { { "Input1", 100 }, { "Input2", 200 } };
            this.manager.RestoreCommittedHashes(restoredHashes, null);

            this.manager.EndRestoration();

            // First solve: inputs may differ from restored (simulating different upstream data)
            var currentHashes = new Dictionary<string, int> { { "Input1", 999 }, { "Input2", 888 } };
            this.manager.UpdatePendingHashes(currentHashes);

            // GetChangedInputs should return empty due to suppression
            var changed = this.manager.GetChangedInputs();
            Assert.Empty(changed);

            // Clear suppression after first solve
            this.manager.ClearSuppressionAfterFirstSolve();

            // Now changes should be detected
            changed = this.manager.GetChangedInputs();
            Assert.Equal(2, changed.Count);
        }

        #endregion

        #region Debounce Tests

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: StartDebounce with zero ms transitions immediately [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: StartDebounce with zero ms transitions immediately [Core]")]
#endif
        public void StartDebounce_ZeroMs_TransitionsImmediately()
        {
            this.manager.StartDebounce(ComponentState.NeedsRun, 0);

            Assert.Equal(ComponentState.NeedsRun, this.manager.CurrentState);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: StartDebounce fires event [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: StartDebounce fires event [Core]")]
#endif
        public void StartDebounce_FiresEvent()
        {
            ComponentState? targetState = null;
            int? milliseconds = null;
            this.manager.DebounceStarted += (state, ms) =>
            {
                targetState = state;
                milliseconds = ms;
            };

            this.manager.StartDebounce(ComponentState.NeedsRun, 100);

            Assert.Equal(ComponentState.NeedsRun, targetState);
            Assert.Equal(100, milliseconds);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: CancelDebounce fires event [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: CancelDebounce fires event [Core]")]
#endif
        public void CancelDebounce_FiresEvent()
        {
            var cancelled = false;
            this.manager.DebounceCancelled += () => cancelled = true;

            this.manager.StartDebounce(ComponentState.NeedsRun, 1000);
            this.manager.CancelDebounce();

            Assert.True(cancelled);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: Debounce timer transitions after delay [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: Debounce timer transitions after delay [Core]")]
#endif
        public async Task DebounceTimer_TransitionsAfterDelay()
        {
            this.manager.StartDebounce(ComponentState.NeedsRun, 50);

            // State should still be Completed immediately
            Assert.Equal(ComponentState.Completed, this.manager.CurrentState);

            // Wait for debounce
            await Task.Delay(100);

            Assert.Equal(ComponentState.NeedsRun, this.manager.CurrentState);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: CancelDebounce prevents transition [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: CancelDebounce prevents transition [Core]")]
#endif
        public async Task CancelDebounce_PreventsTransition()
        {
            this.manager.StartDebounce(ComponentState.NeedsRun, 100);
            this.manager.CancelDebounce();

            await Task.Delay(150);

            Assert.Equal(ComponentState.Completed, this.manager.CurrentState);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: Restarting debounce invalidates previous [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: Restarting debounce invalidates previous [Core]")]
#endif
        public async Task RestartDebounce_InvalidatesPrevious()
        {
            // Start debounce to NeedsRun
            this.manager.StartDebounce(ComponentState.NeedsRun, 50);

            // Immediately restart to Processing
            this.manager.StartDebounce(ComponentState.Processing, 50);

            await Task.Delay(100);

            // Should be Processing, not NeedsRun
            Assert.Equal(ComponentState.Processing, this.manager.CurrentState);
        }

        #endregion

        #region Reset Tests

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: Reset restores initial state [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: Reset restores initial state [Core]")]
#endif
        public void Reset_RestoresInitialState()
        {
            // Setup: modify state
            this.manager.ForceState(ComponentState.Processing);
            this.manager.UpdatePendingHashes(new Dictionary<string, int> { { "Input1", 123 } });
            this.manager.CommitHashes();
            this.manager.BeginRestoration();

            // Reset
            this.manager.Reset();

            Assert.Equal(ComponentState.Completed, this.manager.CurrentState);
            Assert.False(this.manager.IsRestoringFromFile);
            Assert.False(this.manager.IsSuppressingInputChanges);
            Assert.Empty(this.manager.GetCommittedHashes());
        }

        #endregion

        #region Dispose Tests

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: Dispose prevents further operations [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: Dispose prevents further operations [Core]")]
#endif
        public void Dispose_PreventsFurtherOperations()
        {
            this.manager.Dispose();

            Assert.Throws<ObjectDisposedException>(() =>
                this.manager.RequestTransition(ComponentState.NeedsRun, TransitionReason.InputChanged));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: Double dispose is safe [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: Double dispose is safe [Core]")]
#endif
        public void DoubleDispose_IsSafe()
        {
            this.manager.Dispose();
            this.manager.Dispose(); // Should not throw
        }

        #endregion

        #region Edge Case Tests

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: Null hashes are handled gracefully [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: Null hashes are handled gracefully [Core]")]
#endif
        public void NullHashes_HandledGracefully()
        {
            this.manager.UpdatePendingHashes(null);
            this.manager.UpdatePendingBranchCounts(null);
            this.manager.RestoreCommittedHashes(null, null);

            // Should not throw, and hashes should be empty
            Assert.Empty(this.manager.GetCommittedHashes());
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: ClearPendingTransitions works during idle [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: ClearPendingTransitions works during idle [Core]")]
#endif
        public void ClearPendingTransitions_WorksDuringIdle()
        {
            this.manager.ClearPendingTransitions();
            Assert.Equal(0, this.manager.PendingTransitionCount);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: Component name is used for logging [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: Component name is used for logging [Core]")]
#endif
        public void ComponentName_UsedForLogging()
        {
            var namedManager = new ComponentStateManager("MyTestComponent");
            // This test just verifies no exception is thrown - logging is debug output
            namedManager.RequestTransition(ComponentState.NeedsRun, TransitionReason.InputChanged);
            namedManager.Dispose();
        }

        #endregion

        #region Concurrent Access Tests

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: Concurrent transitions are thread-safe [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: Concurrent transitions are thread-safe [Core]")]
#endif
        public async Task ConcurrentTransitions_AreThreadSafe()
        {
            var tasks = new List<Task>();
            var transitionCount = 0;

            this.manager.StateChanged += (old, newState) => Interlocked.Increment(ref transitionCount);

            // Start multiple transitions concurrently
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    // Try various transitions - some will be valid, some won't
                    this.manager.RequestTransition(ComponentState.NeedsRun, TransitionReason.InputChanged);
                    this.manager.RequestTransition(ComponentState.Processing, TransitionReason.RunEnabled);
                }));
            }

            await Task.WhenAll(tasks);

            // Should have at least some successful transitions, and no exceptions
            Assert.True(transitionCount >= 1);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ComponentStateManager: Concurrent hash updates are thread-safe [Windows]")]
#else
        [Fact(DisplayName = "ComponentStateManager: Concurrent hash updates are thread-safe [Core]")]
#endif
        public async Task ConcurrentHashUpdates_AreThreadSafe()
        {
            var tasks = new List<Task>();

            for (int i = 0; i < 10; i++)
            {
                var index = i;
                tasks.Add(Task.Run(() =>
                {
                    var hashes = new Dictionary<string, int> { { $"Input{index}", index * 100 } };
                    this.manager.UpdatePendingHashes(hashes);
                    this.manager.CommitHashes();
                    _ = this.manager.GetChangedInputs();
                }));
            }

            await Task.WhenAll(tasks);

            // Should complete without exceptions
            Assert.NotNull(this.manager.GetCommittedHashes());
        }

        #endregion
    }
}
