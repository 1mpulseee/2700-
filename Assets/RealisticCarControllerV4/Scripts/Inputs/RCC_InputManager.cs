//----------------------------------------------
//            Realistic Car Controller
//
// Copyright © 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// The primary input management system for RCC, handling vehicle, camera, and optional inputs. 
/// This class translates raw input actions (from Unity's InputSystem) into RCC-friendly variables 
/// and events that other systems (e.g., RCC_CarControllerV4, RCC_Camera, RCC_MobileButtons) can access.
/// </summary>
public class RCC_InputManager : RCC_Singleton<RCC_InputManager> {

    /// <summary>
    /// An instance of RCC_Inputs, which aggregates all relevant input values (throttle, brake, etc.).
    /// </summary>
    public RCC_Inputs inputs = new RCC_Inputs();

    /// <summary>
    /// The InputActions asset for RCC. It’s assigned and enabled once in GetInputs().
    /// </summary>
    private static RCC_InputActions inputActions;

    /// <summary>
    /// Indicates whether gyroscopic (mobile) steering is in use.
    /// </summary>
    public bool gyroUsed = false;

    #region Events and Delegates

    public delegate void onStartStopEngine();
    /// <summary>
    /// Invoked when the engine toggle (start/stop) is performed.
    /// </summary>
    public static event onStartStopEngine OnStartStopEngine;

    public delegate void onLowBeamHeadlights();
    /// <summary>
    /// Invoked when toggling low-beam headlights.
    /// </summary>
    public static event onLowBeamHeadlights OnLowBeamHeadlights;

    public delegate void onHighBeamHeadlights();
    /// <summary>
    /// Invoked when toggling high-beam headlights.
    /// </summary>
    public static event onHighBeamHeadlights OnHighBeamHeadlights;

    public delegate void onChangeCamera();
    /// <summary>
    /// Invoked when requesting a camera mode change.
    /// </summary>
    public static event onChangeCamera OnChangeCamera;

    public delegate void onIndicatorLeft();
    /// <summary>
    /// Invoked when toggling the left indicator.
    /// </summary>
    public static event onIndicatorLeft OnIndicatorLeft;

    public delegate void onIndicatorRight();
    /// <summary>
    /// Invoked when toggling the right indicator.
    /// </summary>
    public static event onIndicatorRight OnIndicatorRight;

    public delegate void onIndicatorHazard();
    /// <summary>
    /// Invoked when toggling the hazard lights.
    /// </summary>
    public static event onIndicatorHazard OnIndicatorHazard;

    public delegate void onInteriorlights();
    /// <summary>
    /// Invoked when toggling the interior lights.
    /// </summary>
    public static event onInteriorlights OnInteriorlights;

    public delegate void onGearShiftUp();
    /// <summary>
    /// Invoked when shifting up a gear.
    /// </summary>
    public static event onGearShiftUp OnGearShiftUp;

    public delegate void onGearShiftDown();
    /// <summary>
    /// Invoked when shifting down a gear.
    /// </summary>
    public static event onGearShiftDown OnGearShiftDown;

    public delegate void onNGear(bool state);
    /// <summary>
    /// Invoked when toggling neutral gear, passing a boolean that indicates the gear state.
    /// </summary>
    public static event onNGear OnNGear;

    public delegate void onSlowMotion(bool state);
    /// <summary>
    /// Invoked when toggling slow-motion gameplay, passing a boolean to indicate activation/deactivation.
    /// </summary>
    public static event onSlowMotion OnSlowMotion;

    public delegate void onRecord();
    /// <summary>
    /// Invoked when starting a recording session.
    /// </summary>
    public static event onRecord OnRecord;

    public delegate void onReplay();
    /// <summary>
    /// Invoked when replaying a previous recording.
    /// </summary>
    public static event onReplay OnReplay;

    public delegate void onLookBack(bool state);
    /// <summary>
    /// Invoked when toggling the look-back camera, passing a boolean for its on/off state.
    /// </summary>
    public static event onLookBack OnLookBack;

    public delegate void onTrailerDetach();
    /// <summary>
    /// Invoked when detaching a trailer from the vehicle.
    /// </summary>
    public static event onTrailerDetach OnTrailerDetach;

    #endregion

    private void Awake() {

        // Hide this GameObject from the scene hierarchy for cleanliness.
        gameObject.hideFlags = HideFlags.HideInHierarchy;

        // Instantiate the inputs container.
        inputs = new RCC_Inputs();

    }

    private void Update() {

        // Create or re-initialize the inputs structure if null.
        if (inputs == null)
            inputs = new RCC_Inputs();

        // Collect current frame inputs from the InputSystem (or mobile, if enabled).
        GetInputs();

    }

    /// <summary>
    /// Initializes and updates all input mappings, registering callbacks 
    /// for button/axis events from RCC_InputActions.
    /// </summary>
    public void GetInputs() {

        // If we haven't initialized the InputActions asset yet, do so and bind events.
        if (inputActions == null) {

            inputActions = new RCC_InputActions();
            inputActions.Enable();

            // General vehicle events
            inputActions.Vehicle.StartStopEngine.performed += StartStopEngine_performed;
            inputActions.Vehicle.LowBeamLights.performed += LowBeamLights_performed;
            inputActions.Vehicle.HighBeamLights.performed += HighBeamLights_performed;
            inputActions.Camera.ChangeCamera.performed += ChangeCamera_performed;
            inputActions.Vehicle.IndicatorLeft.performed += IndicatorLeft_performed;
            inputActions.Vehicle.IndicatorRight.performed += IndicatorRight_performed;
            inputActions.Vehicle.IndicatorHazard.performed += IndicatorHazard_performed;
            inputActions.Vehicle.InteriorLights.performed += InteriorLights_performed;
            inputActions.Vehicle.GearShiftUp.performed += GearShiftUp_performed;
            inputActions.Vehicle.GearShiftDown.performed += GearShiftDown_performed;
            inputActions.Vehicle.NGear.performed += NGear_performed;
            inputActions.Vehicle.NGear.canceled += NGear_canceled;
            inputActions.Optional.SlowMotion.performed += SlowMotion_performed;
            inputActions.Optional.SlowMotion.canceled += SlowMotion_canceled;
            inputActions.Optional.Record.performed += Record_performed;
            inputActions.Optional.Replay.performed += Replay_performed;
            inputActions.Camera.LookBack.performed += LookBack_performed;
            inputActions.Camera.LookBack.canceled += LookBack_canceled;
            inputActions.Vehicle.TrailerDetach.performed += TrailerDetach_performed;

        }

        // If the mobile controller is disabled, read standard input from the InputActions asset.
        if (!Settings.mobileControllerEnabled) {

            inputs.throttleInput = inputActions.Vehicle.Throttle.ReadValue<float>();
            inputs.brakeInput = inputActions.Vehicle.Brake.ReadValue<float>();
            inputs.steerInput = inputActions.Vehicle.Steering.ReadValue<float>();
            inputs.handbrakeInput = inputActions.Vehicle.Handbrake.ReadValue<float>();
            inputs.boostInput = inputActions.Vehicle.NOS.ReadValue<float>();
            inputs.clutchInput = inputActions.Vehicle.Clutch.ReadValue<float>();

            // Camera orbit/zoom inputs (e.g., right-stick or mouse movement).
            inputs.orbitX = inputActions.Camera.Orbit.ReadValue<Vector2>().x;
            inputs.orbitY = inputActions.Camera.Orbit.ReadValue<Vector2>().y;
            inputs.scroll = inputActions.Camera.Zoom.ReadValue<Vector2>();

        } else {
            // If using mobile controls, read from RCC_MobileButtons.
            inputs.throttleInput = RCC_MobileButtons.mobileInputs.throttleInput;
            inputs.brakeInput = RCC_MobileButtons.mobileInputs.brakeInput;
            inputs.steerInput = RCC_MobileButtons.mobileInputs.steerInput;
            inputs.handbrakeInput = RCC_MobileButtons.mobileInputs.handbrakeInput;
            inputs.boostInput = RCC_MobileButtons.mobileInputs.boostInput;
        }

    }

    #region Callback Methods

    private void StartStopEngine_performed(InputAction.CallbackContext obj) {

        OnStartStopEngine?.Invoke();

    }

    private void TrailerDetach_performed(InputAction.CallbackContext obj) {

        OnTrailerDetach?.Invoke();

    }

    private void LookBack_performed(InputAction.CallbackContext obj) {

        OnLookBack?.Invoke(true);

    }

    private void LookBack_canceled(InputAction.CallbackContext obj) {

        OnLookBack?.Invoke(false);

    }

    private static void Replay_performed(InputAction.CallbackContext obj) {

        OnReplay?.Invoke();

    }

    private static void Record_performed(InputAction.CallbackContext obj) {

        OnRecord?.Invoke();

    }

    private static void SlowMotion_performed(InputAction.CallbackContext obj) {

        OnSlowMotion?.Invoke(true);

    }

    private static void SlowMotion_canceled(InputAction.CallbackContext obj) {

        OnSlowMotion?.Invoke(false);

    }

    private static void NGear_performed(InputAction.CallbackContext obj) {

        OnNGear?.Invoke(true);

    }

    private static void NGear_canceled(InputAction.CallbackContext obj) {

        OnNGear?.Invoke(false);

    }

    private static void GearShiftDown_performed(InputAction.CallbackContext obj) {

        OnGearShiftDown?.Invoke();

    }

    private static void GearShiftUp_performed(InputAction.CallbackContext obj) {

        OnGearShiftUp?.Invoke();

    }

    private static void IndicatorHazard_performed(InputAction.CallbackContext obj) {

        OnIndicatorHazard?.Invoke();

    }

    private static void IndicatorRight_performed(InputAction.CallbackContext obj) {

        OnIndicatorRight?.Invoke();

    }

    private static void IndicatorLeft_performed(InputAction.CallbackContext obj) {

        OnIndicatorLeft?.Invoke();

    }

    private static void ChangeCamera_performed(InputAction.CallbackContext obj) {

        OnChangeCamera?.Invoke();

    }

    private static void HighBeamLights_performed(InputAction.CallbackContext obj) {

        OnHighBeamHeadlights?.Invoke();

    }

    private static void LowBeamLights_performed(InputAction.CallbackContext obj) {

        OnLowBeamHeadlights?.Invoke();

    }

    private static void InteriorLights_performed(InputAction.CallbackContext obj) {

        OnInteriorlights?.Invoke();

    }

    #endregion

}
