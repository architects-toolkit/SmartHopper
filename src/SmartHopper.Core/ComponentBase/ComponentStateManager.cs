/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Specifies the reason for a state transition.
    /// Used for debugging and logging purposes.
    /// </summary>
    public enum TransitionReason
    {
        /// <summary>
        /// Initial state or unknown reason.
        /// </summary>
        Initial,

        /// <summary>
        /// Input values have changed.
        /// </summary>
        InputChanged,

        /// <summary>
        /// The Run parameter was enabled.
        /// </summary>
        RunEnabled,

        /// <summary>
        /// The Run parameter was disabled.
        /// </summary>
        RunDisabled,

        /// <summary>
        /// Debounce timer completed.
        /// </summary>
        DebounceComplete,

        /// <summary>
        /// Processing has completed successfully.
        /// </summary>
        ProcessingComplete,

        /// <summary>
        /// Processing was cancelled by user.
        /// </summary>
        Cancelled,

        /// <summary>
        /// An error occurred during processing.
        /// </summary>
        Error,

        /// <summary>
        /// File restoration triggered transition.
        /// </summary>
        FileRestoration,
    }

    /// <summary>
    /// Represents a request to transition to a new state.
    /// </summary>
    public sealed class StateTransitionRequest
    {
        /// <summary>
        /// Gets the target state for this transition.
        /// </summary>
        public ComponentState TargetState { get; }

        /// <summary>
        /// Gets the reason for this transition.
        /// </summary>
        public TransitionReason Reason { get; }

        /// <summary>
        /// Gets the timestamp when this request was created.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="StateTransitionRequest"/> class.
        /// </summary>
        /// <param name="targetState">The target state.</param>
        /// <param name="reason">The reason for transition.</param>
        public StateTransitionRequest(ComponentState targetState, TransitionReason reason)
        {
            this.TargetState = targetState;
            this.Reason = reason;
            this.Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Centralized state manager for stateful async components.
    /// Handles state transitions, debouncing, and persistence coordination.
    /// Thread-safe and designed for UI thread execution.
    /// </summary>
    public sealed class ComponentStateManager : IDisposable
    {
        #region Fields

        private readonly object stateLock = new object();
        private readonly object hashLock = new object();
        private readonly Queue<StateTransitionRequest> pendingTransitions = new Queue<StateTransitionRequest>();

        private ComponentState currentState = ComponentState.Completed;
        private bool isTransitioning;
        private bool isDisposed;

        // Restoration state
        private bool isRestoringFromFile;
        private bool suppressInputChangeDetection;

        // Debounce state
        private Timer debounceTimer;
        private int debounceGeneration;
        private ComponentState debounceTargetState;
        private int debounceTimeMs;

        // Hash tracking
        private Dictionary<string, int> committedInputHashes = new Dictionary<string, int>();
        private Dictionary<string, int> pendingInputHashes = new Dictionary<string, int>();
        private Dictionary<string, int> committedBranchCounts = new Dictionary<string, int>();
        private Dictionary<string, int> pendingBranchCounts = new Dictionary<string, int>();

        // Component name for logging
        private readonly string componentName;

        #endregion

        #region Events

        /// <summary>
        /// Occurs when the state changes from one state to another.
        /// Parameters: (oldState, newState).
        /// </summary>
        public event Action<ComponentState, ComponentState> StateChanged;

        /// <summary>
        /// Occurs when entering a new state.
        /// Parameter: newState.
        /// </summary>
        public event Action<ComponentState> StateEntered;

        /// <summary>
        /// Occurs when exiting a state.
        /// Parameter: oldState.
        /// </summary>
        public event Action<ComponentState> StateExited;

        /// <summary>
        /// Occurs when a debounce timer starts.
        /// Parameters: (targetState, milliseconds).
        /// </summary>
        public event Action<ComponentState, int> DebounceStarted;

        /// <summary>
        /// Occurs when a debounce timer is cancelled.
        /// </summary>
        public event Action DebounceCancelled;

        /// <summary>
        /// Occurs when a state transition request is rejected.
        /// Parameters: (currentState, requestedState, reason).
        /// </summary>
        public event Action<ComponentState, ComponentState, string> TransitionRejected;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current component state.
        /// </summary>
        public ComponentState CurrentState
        {
            get
            {
                lock (this.stateLock)
                {
                    return this.currentState;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether a state transition is currently in progress.
        /// </summary>
        public bool IsTransitioning
        {
            get
            {
                lock (this.stateLock)
                {
                    return this.isTransitioning;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether file restoration is in progress.
        /// </summary>
        public bool IsRestoringFromFile
        {
            get
            {
                lock (this.stateLock)
                {
                    return this.isRestoringFromFile;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether input change detection is currently suppressed.
        /// </summary>
        public bool IsSuppressingInputChanges
        {
            get
            {
                lock (this.stateLock)
                {
                    return this.suppressInputChangeDetection;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether a debounce timer is currently active.
        /// </summary>
        public bool IsDebouncing
        {
            get
            {
                lock (this.stateLock)
                {
                    return this.debounceGeneration > 0 && this.debounceTimeMs > 0;
                }
            }
        }

        /// <summary>
        /// Gets the number of pending transitions in the queue.
        /// </summary>
        public int PendingTransitionCount
        {
            get
            {
                lock (this.stateLock)
                {
                    return this.pendingTransitions.Count;
                }
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ComponentStateManager"/> class.
        /// </summary>
        /// <param name="componentName">Optional name for logging purposes.</param>
        public ComponentStateManager(string componentName = null)
        {
            this.componentName = componentName ?? "Component";
            this.debounceTimer = new Timer(this.OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);
        }

        #endregion

        #region State Transitions

        /// <summary>
        /// Requests a state transition. Transitions are queued and processed in order.
        /// </summary>
        /// <param name="newState">The target state.</param>
        /// <param name="reason">The reason for the transition.</param>
        /// <returns>True if the transition was queued or processed; false if rejected.</returns>
        public bool RequestTransition(ComponentState newState, TransitionReason reason)
        {
            this.ThrowIfDisposed();

            lock (this.stateLock)
            {
                // Validate the transition
                if (!this.IsValidTransition(this.currentState, newState))
                {
                    var message = $"Invalid transition from {this.currentState} to {newState}";
                    Debug.WriteLine($"[{this.componentName}] {message}");
                    this.TransitionRejected?.Invoke(this.currentState, newState, message);
                    return false;
                }

                // Queue the transition
                var request = new StateTransitionRequest(newState, reason);
                this.pendingTransitions.Enqueue(request);
                Debug.WriteLine($"[{this.componentName}] Queued transition to {newState} (reason: {reason})");

                // Process queue if not already transitioning
                if (!this.isTransitioning)
                {
                    this.ProcessTransitionQueue();
                }

                return true;
            }
        }

        /// <summary>
        /// Processes the transition queue, executing each transition in order.
        /// </summary>
        private void ProcessTransitionQueue()
        {
            // Already holding stateLock from caller
            if (this.isTransitioning)
            {
                return;
            }

            this.isTransitioning = true;

            try
            {
                while (this.pendingTransitions.Count > 0)
                {
                    var request = this.pendingTransitions.Dequeue();
                    this.ExecuteTransition(request);
                }
            }
            finally
            {
                this.isTransitioning = false;
            }
        }

        /// <summary>
        /// Executes a single state transition.
        /// </summary>
        /// <param name="request">The transition request to execute.</param>
        private void ExecuteTransition(StateTransitionRequest request)
        {
            // Already holding stateLock from caller
            var oldState = this.currentState;
            var newState = request.TargetState;

            // Skip if already in target state
            if (oldState == newState)
            {
                Debug.WriteLine($"[{this.componentName}] Already in state {newState}, skipping transition");
                return;
            }

            // Re-validate (state may have changed since queuing)
            if (!this.IsValidTransition(oldState, newState))
            {
                var message = $"Transition from {oldState} to {newState} no longer valid";
                Debug.WriteLine($"[{this.componentName}] {message}");
                this.TransitionRejected?.Invoke(oldState, newState, message);
                return;
            }

            Debug.WriteLine($"[{this.componentName}] Transitioning: {oldState} -> {newState} (reason: {request.Reason})");

            // Fire exit event
            this.StateExited?.Invoke(oldState);

            // Update state
            this.currentState = newState;

            // Fire enter event
            this.StateEntered?.Invoke(newState);

            // Fire changed event
            this.StateChanged?.Invoke(oldState, newState);
        }

        /// <summary>
        /// Checks if a transition from one state to another is valid.
        /// </summary>
        /// <param name="from">The source state.</param>
        /// <param name="to">The target state.</param>
        /// <returns>True if the transition is valid; otherwise false.</returns>
        public bool IsValidTransition(ComponentState from, ComponentState to)
        {
            // Same state is not a transition
            if (from == to)
            {
                return false;
            }

            // Define valid transitions based on state machine
            switch (from)
            {
                case ComponentState.Completed:
                    return to == ComponentState.Waiting
                        || to == ComponentState.NeedsRun
                        || to == ComponentState.Processing
                        || to == ComponentState.Error;

                case ComponentState.Waiting:
                    return to == ComponentState.NeedsRun
                        || to == ComponentState.Processing
                        || to == ComponentState.Error;

                case ComponentState.NeedsRun:
                    return to == ComponentState.Processing
                        || to == ComponentState.Error;

                case ComponentState.Processing:
                    return to == ComponentState.Completed
                        || to == ComponentState.Cancelled
                        || to == ComponentState.Error;

                case ComponentState.Cancelled:
                    return to == ComponentState.Waiting
                        || to == ComponentState.NeedsRun
                        || to == ComponentState.Processing
                        || to == ComponentState.Error;

                case ComponentState.Error:
                    return to == ComponentState.Waiting
                        || to == ComponentState.NeedsRun
                        || to == ComponentState.Processing
                        || to == ComponentState.Error;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Forces an immediate state change without validation or queueing.
        /// Use with caution - primarily for initialization and testing.
        /// </summary>
        /// <param name="newState">The new state to set.</param>
        public void ForceState(ComponentState newState)
        {
            this.ThrowIfDisposed();

            lock (this.stateLock)
            {
                var oldState = this.currentState;
                if (oldState == newState)
                {
                    return;
                }

                Debug.WriteLine($"[{this.componentName}] Force state: {oldState} -> {newState}");

                this.StateExited?.Invoke(oldState);
                this.currentState = newState;
                this.StateEntered?.Invoke(newState);
                this.StateChanged?.Invoke(oldState, newState);
            }
        }

        /// <summary>
        /// Clears any pending transitions from the queue.
        /// </summary>
        public void ClearPendingTransitions()
        {
            lock (this.stateLock)
            {
                var count = this.pendingTransitions.Count;
                this.pendingTransitions.Clear();
                if (count > 0)
                {
                    Debug.WriteLine($"[{this.componentName}] Cleared {count} pending transitions");
                }
            }
        }

        #endregion

        #region File Restoration

        /// <summary>
        /// Marks the beginning of file restoration. Suppresses input change detection.
        /// Call this at the start of Read() method.
        /// </summary>
        public void BeginRestoration()
        {
            this.ThrowIfDisposed();

            lock (this.stateLock)
            {
                Debug.WriteLine($"[{this.componentName}] Begin restoration");
                this.isRestoringFromFile = true;
                this.suppressInputChangeDetection = true;

                // Clear pending hashes during restoration
                lock (this.hashLock)
                {
                    this.pendingInputHashes.Clear();
                    this.pendingBranchCounts.Clear();
                }
            }
        }

        /// <summary>
        /// Marks the end of file restoration. The first solve after this will
        /// skip input change detection, then normal detection resumes.
        /// Call this at the end of Read() method.
        /// </summary>
        public void EndRestoration()
        {
            this.ThrowIfDisposed();

            lock (this.stateLock)
            {
                Debug.WriteLine($"[{this.componentName}] End restoration (suppression still active for first solve)");
                this.isRestoringFromFile = false;
                // Note: suppressInputChangeDetection stays true until ClearSuppressionAfterFirstSolve() is called
            }
        }

        /// <summary>
        /// Clears the input change detection suppression.
        /// Call this after the first successful solve following file restoration.
        /// </summary>
        public void ClearSuppressionAfterFirstSolve()
        {
            lock (this.stateLock)
            {
                if (this.suppressInputChangeDetection)
                {
                    Debug.WriteLine($"[{this.componentName}] Clearing input change suppression after first solve");
                    this.suppressInputChangeDetection = false;
                }
            }
        }

        #endregion

        #region Hash Management

        /// <summary>
        /// Updates pending input hashes without triggering state changes.
        /// These hashes represent the current input state.
        /// </summary>
        /// <param name="hashes">Dictionary of input name to hash value.</param>
        public void UpdatePendingHashes(Dictionary<string, int> hashes)
        {
            this.ThrowIfDisposed();

            if (hashes == null)
            {
                return;
            }

            lock (this.hashLock)
            {
                this.pendingInputHashes = new Dictionary<string, int>(hashes);
            }
        }

        /// <summary>
        /// Updates pending branch counts without triggering state changes.
        /// </summary>
        /// <param name="branchCounts">Dictionary of input name to branch count.</param>
        public void UpdatePendingBranchCounts(Dictionary<string, int> branchCounts)
        {
            this.ThrowIfDisposed();

            if (branchCounts == null)
            {
                return;
            }

            lock (this.hashLock)
            {
                this.pendingBranchCounts = new Dictionary<string, int>(branchCounts);
            }
        }

        /// <summary>
        /// Commits pending hashes as the new baseline.
        /// Call this after successful processing to update what "unchanged" means.
        /// </summary>
        public void CommitHashes()
        {
            this.ThrowIfDisposed();

            lock (this.hashLock)
            {
                Debug.WriteLine($"[{this.componentName}] Committing {this.pendingInputHashes.Count} hashes");
                this.committedInputHashes = new Dictionary<string, int>(this.pendingInputHashes);
                this.committedBranchCounts = new Dictionary<string, int>(this.pendingBranchCounts);
            }
        }

        /// <summary>
        /// Restores committed hashes from persisted data.
        /// Used during file restoration to set the baseline.
        /// </summary>
        /// <param name="hashes">The hash values to restore.</param>
        /// <param name="branchCounts">The branch counts to restore.</param>
        public void RestoreCommittedHashes(Dictionary<string, int> hashes, Dictionary<string, int> branchCounts)
        {
            this.ThrowIfDisposed();

            lock (this.hashLock)
            {
                Debug.WriteLine($"[{this.componentName}] Restoring {hashes?.Count ?? 0} committed hashes");
                this.committedInputHashes = hashes != null
                    ? new Dictionary<string, int>(hashes)
                    : new Dictionary<string, int>();
                this.committedBranchCounts = branchCounts != null
                    ? new Dictionary<string, int>(branchCounts)
                    : new Dictionary<string, int>();

                // Also set pending to match committed during restoration
                this.pendingInputHashes = new Dictionary<string, int>(this.committedInputHashes);
                this.pendingBranchCounts = new Dictionary<string, int>(this.committedBranchCounts);
            }
        }

        /// <summary>
        /// Gets the list of input names that have changed since the last commit.
        /// Returns empty list during restoration or when suppression is active.
        /// </summary>
        /// <returns>List of changed input names.</returns>
        public IReadOnlyList<string> GetChangedInputs()
        {
            lock (this.stateLock)
            {
                if (this.suppressInputChangeDetection)
                {
                    Debug.WriteLine($"[{this.componentName}] GetChangedInputs: suppressed, returning empty");
                    return Array.Empty<string>();
                }
            }

            lock (this.hashLock)
            {
                var changed = new List<string>();

                // Check for changed or new inputs
                foreach (var kvp in this.pendingInputHashes)
                {
                    if (!this.committedInputHashes.TryGetValue(kvp.Key, out var committedHash) ||
                        committedHash != kvp.Value)
                    {
                        changed.Add(kvp.Key);
                    }
                }

                // Check for removed inputs
                foreach (var key in this.committedInputHashes.Keys)
                {
                    if (!this.pendingInputHashes.ContainsKey(key))
                    {
                        changed.Add(key);
                    }
                }

                if (changed.Count > 0)
                {
                    Debug.WriteLine($"[{this.componentName}] GetChangedInputs: {string.Join(", ", changed)}");
                }

                return changed;
            }
        }

        /// <summary>
        /// Gets the committed input hashes.
        /// Used for persistence (saving to file).
        /// </summary>
        /// <returns>Copy of the committed hashes dictionary.</returns>
        public Dictionary<string, int> GetCommittedHashes()
        {
            lock (this.hashLock)
            {
                return new Dictionary<string, int>(this.committedInputHashes);
            }
        }

        /// <summary>
        /// Gets the committed branch counts.
        /// Used for persistence (saving to file).
        /// </summary>
        /// <returns>Copy of the committed branch counts dictionary.</returns>
        public Dictionary<string, int> GetCommittedBranchCounts()
        {
            lock (this.hashLock)
            {
                return new Dictionary<string, int>(this.committedBranchCounts);
            }
        }

        /// <summary>
        /// Clears all hash tracking data.
        /// </summary>
        public void ClearHashes()
        {
            lock (this.hashLock)
            {
                this.committedInputHashes.Clear();
                this.pendingInputHashes.Clear();
                this.committedBranchCounts.Clear();
                this.pendingBranchCounts.Clear();
            }
        }

        #endregion

        #region Debounce

        /// <summary>
        /// Starts or restarts the debounce timer.
        /// When the timer elapses, a transition to the target state will be requested.
        /// </summary>
        /// <param name="targetState">The state to transition to after debounce.</param>
        /// <param name="milliseconds">Debounce duration in milliseconds.</param>
        public void StartDebounce(ComponentState targetState, int milliseconds)
        {
            this.ThrowIfDisposed();

            if (milliseconds <= 0)
            {
                // No debounce, transition immediately
                this.RequestTransition(targetState, TransitionReason.InputChanged);
                return;
            }

            lock (this.stateLock)
            {
                // Increment generation to invalidate any pending callbacks
                this.debounceGeneration++;
                this.debounceTargetState = targetState;
                this.debounceTimeMs = milliseconds;

                Debug.WriteLine($"[{this.componentName}] Starting debounce: {milliseconds}ms -> {targetState} (gen: {this.debounceGeneration})");

                // Restart timer
                this.debounceTimer.Change(milliseconds, Timeout.Infinite);

                this.DebounceStarted?.Invoke(targetState, milliseconds);
            }
        }

        /// <summary>
        /// Cancels any pending debounce timer.
        /// </summary>
        public void CancelDebounce()
        {
            lock (this.stateLock)
            {
                if (this.debounceTimeMs > 0)
                {
                    Debug.WriteLine($"[{this.componentName}] Cancelling debounce");
                    this.debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    this.debounceTimeMs = 0;
                    this.debounceGeneration++; // Invalidate any pending callbacks

                    this.DebounceCancelled?.Invoke();
                }
            }
        }

        /// <summary>
        /// Called when the debounce timer elapses.
        /// </summary>
        /// <param name="state">Timer callback state (unused).</param>
        private void OnDebounceElapsed(object state)
        {
            int capturedGeneration;
            ComponentState targetState;
            int capturedTimeMs;

            // Capture all required data in a single lock to ensure consistency
            lock (this.stateLock)
            {
                // If debounce time is 0, timer has already been cancelled/reset
                if (this.debounceTimeMs == 0)
                {
                    Debug.WriteLine($"[{this.componentName}] Debounce callback: timer already cancelled");
                    return;
                }

                capturedGeneration = this.debounceGeneration;
                targetState = this.debounceTargetState;
                capturedTimeMs = this.debounceTimeMs;
                
                // Mark as elapsed immediately to prevent race conditions
                this.debounceTimeMs = 0;
            }

            // Additional validation check outside the lock
            lock (this.stateLock)
            {
                // Double-check generation and that we're still the active timer
                if (capturedGeneration != this.debounceGeneration || capturedTimeMs == 0)
                {
                    Debug.WriteLine($"[{this.componentName}] Debounce callback stale (gen {capturedGeneration} != {this.debounceGeneration}, time {capturedTimeMs}), ignoring");
                    return;
                }

                // Validate target state is still compatible with current state
                if (!this.IsValidTransition(this.currentState, targetState))
                {
                    Debug.WriteLine($"[{this.componentName}] Debounce target {targetState} no longer valid from {this.currentState}");
                    return;
                }
            }

            Debug.WriteLine($"[{this.componentName}] Debounce elapsed, requesting transition to {targetState}");
            this.RequestTransition(targetState, TransitionReason.DebounceComplete);
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Resets the state manager to initial state.
        /// Clears all hashes, pending transitions, and cancels debounce.
        /// </summary>
        public void Reset()
        {
            lock (this.stateLock)
            {
                this.CancelDebounce();
                this.ClearPendingTransitions();
                this.isRestoringFromFile = false;
                this.suppressInputChangeDetection = false;
                this.currentState = ComponentState.Completed;
            }

            this.ClearHashes();

            Debug.WriteLine($"[{this.componentName}] State manager reset");
        }

        /// <summary>
        /// Throws if the manager has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(nameof(ComponentStateManager));
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the state manager, releasing the debounce timer.
        /// </summary>
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            lock (this.stateLock)
            {
                this.isDisposed = true;
                this.debounceTimer?.Dispose();
                this.debounceTimer = null;
                this.pendingTransitions.Clear();
            }
        }

        #endregion
    }
}
