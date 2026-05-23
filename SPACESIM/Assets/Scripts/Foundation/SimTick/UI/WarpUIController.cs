using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceSim.Foundation.SimTick.UI
{
    /// <summary>
    /// Debug UI controller for time-warp behavior in <c>TestVessels.unity</c>
    /// (commit 048 Stage 4). Subscribes to <see cref="WarpController"/>'s
    /// <see cref="WarpController.OnRateChanged"/> and
    /// <see cref="WarpController.OnWarpHalted"/> events to keep the on-screen
    /// rate readout and halt-info readout in sync; routes button clicks and
    /// slider changes into the controller's rate / pause / clear-halt API.
    ///
    /// <para>
    /// <strong>EXPECTS A WARPCONTROLLER IN THE SCENE.</strong> If
    /// <see cref="WarpController.Instance"/> is null at <see cref="Start"/>,
    /// the controller logs a warning and disables its buttons gracefully —
    /// the UI is then inert but does not throw. The singleton check is in
    /// Start rather than Awake because MonoBehaviour.Awake ordering between
    /// <see cref="WarpController"/> (sets <see cref="WarpController.Instance"/>)
    /// and <see cref="WarpUIController"/> (reads it) is not guaranteed by
    /// Unity; deferring to Start uses the lifecycle rule that all Awake
    /// calls complete before any Start runs. Production scenes that include
    /// this UI controller should also include a <see cref="WarpController"/>
    /// GameObject; the Stage 4 setup guide
    /// (<c>docs/stage4_setup_guide.md</c>) covers the scene wiring.
    /// </para>
    ///
    /// <para>
    /// <strong>INSPECTOR-WIRED REFERENCES.</strong> All UI element references
    /// are <see cref="SerializeFieldAttribute"/>-tagged private fields. The
    /// setup guide walks through dragging each Hierarchy element onto its
    /// corresponding field. <see cref="Awake"/> validates that no reference
    /// is null and logs an error for each missing wire so configuration gaps
    /// are visible immediately on entering Play mode.
    /// </para>
    ///
    /// <para>
    /// <strong>TMP PRECEDENT.</strong> This is the first TMP usage in the
    /// codebase. Legacy <c>UnityEngine.UI.Text</c> appears in
    /// <c>TestVesselDriver</c> and <c>TestShiftDriver</c> from Phase 0
    /// debug work. Future UI work should follow this TMP precedent. Phase 0
    /// controllers can migrate to TMP opportunistically when touched;
    /// proactive migration is not required.
    /// </para>
    ///
    /// <para>
    /// <strong>SCOPE.</strong> Phase 4 debug UI only — exercises the
    /// <see cref="WarpController"/> public surface so testers can manually
    /// verify rate changes, pause/resume, target-tick advancement (Stage 2),
    /// and the halt-event flow from predictors (Stage 3). Not intended as
    /// the production Mission Control UI; that work lands in Phase 5+ and
    /// will have its own polished controller.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WarpUIController : MonoBehaviour
    {
        // ----- Inspector-wired references -----

        [Header("Display")]
        [SerializeField] private TextMeshProUGUI rateDisplayText;
        [SerializeField] private TextMeshProUGUI haltDisplayText;

        [Header("Pause / resume")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;

        [Header("Discrete rate buttons")]
        [SerializeField] private Button discrete1xButton;
        [SerializeField] private Button discrete5xButton;
        [SerializeField] private Button discrete10xButton;
        [SerializeField] private Button discrete100xButton;
        [SerializeField] private Button discrete1000xButton;
        [SerializeField] private Button discrete10000xButton;
        [SerializeField] private Button discrete100000xButton;

        [Header("Continuous rate")]
        [SerializeField] private Slider continuousSlider;

        [Header("Halt acknowledgement")]
        [SerializeField] private Button clearHaltButton;

        // ----- Internal state -----

        /// <summary>True when <see cref="Awake"/> validated all references AND
        /// <see cref="Start"/> confirmed <see cref="WarpController.Instance"/>
        /// was available; false in degraded mode. Used to short-circuit button
        /// click handlers so a degraded UI doesn't dereference null fields. The
        /// two-phase validation (Awake for fields, Start for singleton)
        /// sidesteps the MonoBehaviour.Awake ordering race described in the
        /// class doc.</summary>
        private bool _ready;

        // ----- Lifecycle -----

        private void Awake()
        {
            // Field validation only — singleton-dependency check is deferred
            // to Start so MonoBehaviour.Awake ordering between WarpController
            // (sets Instance) and WarpUIController (reads Instance) is not a
            // race condition. All Awake calls complete before any Start call,
            // so by the time Start runs WarpController.Instance is guaranteed
            // set if a WarpController GameObject exists in the scene.
            _ready = ValidateInspectorReferences();
            if (!_ready)
            {
                DisableAllInteractables();
            }
        }

        private void Start()
        {
            if (!_ready) return;

            // Singleton-dependency check deferred from Awake to Start so the
            // MonoBehaviour lifecycle guarantees all Awake calls (including
            // WarpController's, which sets Instance) have completed before
            // this read. Field validation in Awake doesn't depend on Instance.
            if (WarpController.Instance == null)
            {
                Debug.LogWarning(
                    "WarpUIController.Start: WarpController.Instance is null. " +
                    "The UI will render but its buttons will be inert until a " +
                    "WarpController GameObject is present in the scene. See " +
                    "docs/stage4_setup_guide.md for the scene-wiring steps.");
                _ready = false;
                DisableAllInteractables();
                return;
            }

            // Wire button click handlers AFTER the singleton null-check so the
            // handlers (which call into WarpController.Instance methods) are
            // only attached when Instance is known non-null.
            pauseButton.onClick.AddListener(OnPauseClicked);
            resumeButton.onClick.AddListener(OnResumeClicked);
            discrete1xButton.onClick.AddListener(() => OnDiscreteLevelClicked(1));
            discrete5xButton.onClick.AddListener(() => OnDiscreteLevelClicked(5));
            discrete10xButton.onClick.AddListener(() => OnDiscreteLevelClicked(10));
            discrete100xButton.onClick.AddListener(() => OnDiscreteLevelClicked(100));
            discrete1000xButton.onClick.AddListener(() => OnDiscreteLevelClicked(1000));
            discrete10000xButton.onClick.AddListener(() => OnDiscreteLevelClicked(10000));
            discrete100000xButton.onClick.AddListener(() => OnDiscreteLevelClicked(100000));
            continuousSlider.onValueChanged.AddListener(OnContinuousSliderChanged);
            clearHaltButton.onClick.AddListener(OnClearHaltClicked);

            // Subscribe to controller events. Subscription lives in Start
            // (paired with OnDestroy teardown, not OnEnable/OnDisable) because
            // this debug UI is not toggled during gameplay — the simpler
            // Start/OnDestroy pattern is the right tradeoff for the use case.
            // See class doc for why.
            WarpController.Instance.OnRateChanged += OnRateChanged;
            WarpController.Instance.OnWarpHalted += OnWarpHalted;

            // Initialize the rate display from current controller state.
            RefreshRateDisplay(WarpController.Instance.CurrentRate);
            ClearHaltDisplay();
        }

        private void OnDestroy()
        {
            // Paired teardown for the Start-time event subscription. We use
            // OnDestroy rather than OnDisable because this debug UI is not
            // toggled at runtime — see the rationale on the Start subscription
            // call. Null-guard for late-domain-reload and scene-teardown edge
            // cases where Instance has already been destroyed by the time
            // this fires.
            if (WarpController.Instance == null) return;
            WarpController.Instance.OnRateChanged -= OnRateChanged;
            WarpController.Instance.OnWarpHalted -= OnWarpHalted;
        }

        // ----- Inspector-reference validation -----

        /// <summary>Log an error for each null Inspector reference. Returns
        /// true if all references are populated, false otherwise. Called once
        /// from <see cref="Awake"/>; the result drives whether the rest of
        /// the controller is wired up or runs in degraded mode.</summary>
        private bool ValidateInspectorReferences()
        {
            bool ok = true;
            ok &= Require(rateDisplayText, nameof(rateDisplayText));
            ok &= Require(haltDisplayText, nameof(haltDisplayText));
            ok &= Require(pauseButton, nameof(pauseButton));
            ok &= Require(resumeButton, nameof(resumeButton));
            ok &= Require(discrete1xButton, nameof(discrete1xButton));
            ok &= Require(discrete5xButton, nameof(discrete5xButton));
            ok &= Require(discrete10xButton, nameof(discrete10xButton));
            ok &= Require(discrete100xButton, nameof(discrete100xButton));
            ok &= Require(discrete1000xButton, nameof(discrete1000xButton));
            ok &= Require(discrete10000xButton, nameof(discrete10000xButton));
            ok &= Require(discrete100000xButton, nameof(discrete100000xButton));
            ok &= Require(continuousSlider, nameof(continuousSlider));
            ok &= Require(clearHaltButton, nameof(clearHaltButton));
            return ok;
        }

        private bool Require(Object reference, string fieldName)
        {
            if (reference == null)
            {
                Debug.LogError(
                    $"WarpUIController on '{gameObject.name}': Inspector field " +
                    $"'{fieldName}' is null. Wire the reference in the Inspector. " +
                    $"See docs/stage4_setup_guide.md for the full wiring table.");
                return false;
            }
            return true;
        }

        /// <summary>Disable interactivity on every button/slider so a misconfigured
        /// UI doesn't drop click handlers onto null references. Called when
        /// <see cref="Awake"/> field validation fails OR <see cref="Start"/>
        /// finds <see cref="WarpController.Instance"/> null. Safe even when
        /// references themselves are null — each disable is null-guarded.
        /// </summary>
        private void DisableAllInteractables()
        {
            DisableIfPresent(pauseButton);
            DisableIfPresent(resumeButton);
            DisableIfPresent(discrete1xButton);
            DisableIfPresent(discrete5xButton);
            DisableIfPresent(discrete10xButton);
            DisableIfPresent(discrete100xButton);
            DisableIfPresent(discrete1000xButton);
            DisableIfPresent(discrete10000xButton);
            DisableIfPresent(discrete100000xButton);
            DisableIfPresent(clearHaltButton);
            if (continuousSlider != null) continuousSlider.interactable = false;
        }

        private static void DisableIfPresent(Button button)
        {
            if (button != null) button.interactable = false;
        }

        // ----- Button click handlers -----

        private void OnPauseClicked()
        {
            WarpController.Instance.Pause();
        }

        private void OnResumeClicked()
        {
            WarpController.Instance.Resume();
        }

        private void OnDiscreteLevelClicked(long level)
        {
            WarpController.Instance.SetDiscreteLevel(level);
        }

        private void OnContinuousSliderChanged(float value)
        {
            // Slider has Whole Numbers checked in Inspector (see setup guide),
            // but defensively clamp here so a misconfigured slider can't push
            // an out-of-range value through WarpController.SetContinuousRate
            // (which throws ArgumentException on out-of-range input).
            long integerRate = (long)Mathf.Round(value);
            if (integerRate < 1) integerRate = 1;
            if (integerRate > 1000) integerRate = 1000;
            WarpController.Instance.SetContinuousRate(integerRate);
        }

        private void OnClearHaltClicked()
        {
            WarpController.Instance.ClearHalt();
            ClearHaltDisplay();
        }

        // ----- Event subscribers -----

        private void OnRateChanged(WarpRate rate)
        {
            RefreshRateDisplay(rate);
        }

        private void OnWarpHalted(WarpHaltInfo info)
        {
            if (haltDisplayText == null) return;
            haltDisplayText.text =
                $"Halted: {info.HaltReason} at tick {info.HaltTick}\n{info.DiagnosticMessage}";
        }

        // ----- Display helpers -----

        private void RefreshRateDisplay(WarpRate rate)
        {
            if (rateDisplayText == null) return;
            if (rate.IsPaused)
            {
                rateDisplayText.text = "Paused";
                return;
            }
            // v1: denominator is always 1, so {Numerator}x reads cleanly.
            // Future fractional modes (denominator > 1) would format as
            // "{Numerator}/{Denominator}x"; not reachable in v1.
            rateDisplayText.text = $"{rate.Numerator}x";
        }

        private void ClearHaltDisplay()
        {
            if (haltDisplayText == null) return;
            haltDisplayText.text = string.Empty;
        }
    }
}
