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

/// <summary>
/// Provides a record/replay system for RCC vehicles by capturing player inputs, 
/// vehicle transforms, and rigidbody velocities each frame during recording, 
/// then applying them back in sequence during playback.
/// </summary>
public class RCC_Recorder : RCC_Core {

    #region Recorded Clip Definition

    /// <summary>
    /// Wraps all the data needed for a single recording session: 
    /// inputs, transforms, and rigidbody states for each frame.
    /// </summary>
    [System.Serializable]
    public class RecordedClip {

        /// <summary>
        /// A descriptive name for the recording.
        /// </summary>
        public string recordName = "New Record";

        /// <summary>
        /// Frame-by-frame recorded player inputs during the session.
        /// </summary>
        [HideInInspector] public PlayerInput[] inputs;

        /// <summary>
        /// Frame-by-frame recorded positions and rotations of the vehicle.
        /// </summary>
        [HideInInspector] public PlayerTransform[] transforms;

        /// <summary>
        /// Frame-by-frame recorded linear and angular velocities of the vehicle’s rigidbody.
        /// </summary>
        [HideInInspector] public PlayerRigidBody[] rigids;

        /// <summary>
        /// Constructs a RecordedClip from arrays of inputs, transforms, and rigidbody data.
        /// </summary>
        public RecordedClip(PlayerInput[] _inputs, PlayerTransform[] _transforms, PlayerRigidBody[] _rigids, string _recordName) {

            inputs = _inputs;
            transforms = _transforms;
            rigids = _rigids;
            recordName = _recordName;

        }

    }

    /// <summary>
    /// The most recently recorded clip.
    /// </summary>
    public RecordedClip recorded;

    #endregion

    #region Temporary Data Structures for Ongoing Record

    /// <summary>
    /// A running list of player inputs during a recording session.
    /// </summary>
    public List<PlayerInput> Inputs;

    /// <summary>
    /// A running list of vehicle transforms (position & rotation) recorded each frame.
    /// </summary>
    public List<PlayerTransform> Transforms;

    /// <summary>
    /// A running list of vehicle rigidbody states (velocity, angular velocity) recorded each frame.
    /// </summary>
    public List<PlayerRigidBody> Rigidbodies;

    #endregion

    #region Player Data Structures

    /// <summary>
    /// Captures a single frame's worth of input data from the vehicle.
    /// </summary>
    [System.Serializable]
    public class PlayerInput {

        public float throttleInput;
        public float brakeInput;
        public float steerInput;
        public float handbrakeInput;
        public float clutchInput;
        public float boostInput;
        public float fuelInput;
        public int direction;
        public bool canGoReverse;
        public int currentGear;
        public bool changingGear;
        public RCC_CarControllerV4.IndicatorsOn indicatorsOn;
        public bool lowBeamHeadLightsOn;
        public bool highBeamHeadLightsOn;

        public PlayerInput(float _gasInput, float _brakeInput, float _steerInput, float _handbrakeInput, float _clutchInput,
                           float _boostInput, float _fuelInput, int _direction, bool _canGoReverse, int _currentGear,
                           bool _changingGear, RCC_CarControllerV4.IndicatorsOn _indicatorsOn,
                           bool _lowBeamHeadLightsOn, bool _highBeamHeadLightsOn) {

            throttleInput = _gasInput;
            brakeInput = _brakeInput;
            steerInput = _steerInput;
            handbrakeInput = _handbrakeInput;
            clutchInput = _clutchInput;
            boostInput = _boostInput;
            fuelInput = _fuelInput;
            direction = _direction;
            canGoReverse = _canGoReverse;
            currentGear = _currentGear;
            changingGear = _changingGear;
            indicatorsOn = _indicatorsOn;
            lowBeamHeadLightsOn = _lowBeamHeadLightsOn;
            highBeamHeadLightsOn = _highBeamHeadLightsOn;

        }

    }

    /// <summary>
    /// Captures a single frame's position and rotation of the vehicle.
    /// </summary>
    [System.Serializable]
    public class PlayerTransform {

        public Vector3 position;
        public Quaternion rotation;

        public PlayerTransform(Vector3 _pos, Quaternion _rot) {

            position = _pos;
            rotation = _rot;

        }

    }

    /// <summary>
    /// Captures a single frame's velocity and angular velocity of the vehicle's rigidbody.
    /// </summary>
    [System.Serializable]
    public class PlayerRigidBody {

        public Vector3 velocity;
        public Vector3 angularVelocity;

        public PlayerRigidBody(Vector3 _vel, Vector3 _angVel) {

            velocity = _vel;
            angularVelocity = _angVel;

        }

    }

    #endregion

    #region Recording Modes

    /// <summary>
    /// The state machine controlling record and playback operations.
    /// </summary>
    public enum Mode {

        Neutral,  // Idle, not recording or playing.
        Play,     // Currently replaying a recorded session.
        Record    // Currently recording a new session.

    }
    public Mode mode = Mode.Neutral;

    #endregion

    private void Awake() {

        // Initialize lists for inputs, transforms, and rigidbody data.
        Inputs = new List<PlayerInput>();
        Transforms = new List<PlayerTransform>();
        Rigidbodies = new List<PlayerRigidBody>();

    }

    #region Recording

    /// <summary>
    /// Toggles the recording state. If turning on, clears any previous data. 
    /// If turning off, saves the current clip into <c>recorded</c> and RBC_Records.
    /// </summary>
    public void Record() {

        // Switch from Neutral/Play -> Record, or Record -> Neutral.
        if (mode != Mode.Record) {

            mode = Mode.Record;

        } else {

            mode = Mode.Neutral;
            SaveRecord();

        }

        // If we just switched into Record mode, clear old data to start fresh.
        if (mode == Mode.Record) {

            Inputs.Clear();
            Transforms.Clear();
            Rigidbodies.Clear();

        }

    }

    /// <summary>
    /// Saves the currently recorded data into a RecordedClip and appends it to RCC_Records.
    /// </summary>
    public void SaveRecord() {

        Debug.Log("Record saved!");

        // Build a new clip from the lists of data gathered this session.
        recorded = new RecordedClip(
            Inputs.ToArray(),
            Transforms.ToArray(),
            Rigidbodies.ToArray(),
            RCC_Records.Instance.records.Count.ToString() + "_" + CarController.transform.name
        );

        // Add the newly constructed clip to the global RCC_Records list.
        RCC_Records.Instance.records.Add(recorded);

    }

    #endregion

    #region Playback

    /// <summary>
    /// Toggles playback of the last recorded clip. If no clip exists, do nothing.
    /// </summary>
    public void Play() {

        // If no recorded session is available, skip.
        if (recorded == null)
            return;

        // Switch from Neutral/Record -> Play, or Play -> Neutral.
        if (mode != Mode.Play)
            mode = Mode.Play;
        else
            mode = Mode.Neutral;

        // If we are now in Play mode, enable externalController to override normal controls.
        CarController.externalController = (mode == Mode.Play);

        if (mode == Mode.Play) {
            // Start replay routines.
            StartCoroutine(Replay());

            // Optionally set the vehicle to the initial position and rotation of the clip.
            if (recorded.transforms.Length > 0) {
                CarController.transform.SetPositionAndRotation(
                    recorded.transforms[0].position,
                    recorded.transforms[0].rotation
                );
            }

            // Start a parallel coroutine to handle updating rigidbody velocities.
            StartCoroutine(Revel());

        }

    }

    /// <summary>
    /// Plays back a specific RecordedClip. Replaces any existing <c>recorded</c> data.
    /// </summary>
    /// <param name="_recorded">The clip to load and play.</param>
    public void Play(RecordedClip _recorded) {

        recorded = _recorded;

        Debug.Log("Replaying record " + recorded.recordName);

        // If no data is present, skip.
        if (recorded == null)
            return;

        // Toggle the Play mode similarly to the other Play() method.
        if (mode != Mode.Play)
            mode = Mode.Play;
        else
            mode = Mode.Neutral;

        CarController.externalController = (mode == Mode.Play);

        if (mode == Mode.Play) {

            // Kick off replaying and set the vehicle's initial transform.
            StartCoroutine(Replay());

            if (recorded.transforms.Length > 0) {

                CarController.transform.SetPositionAndRotation(
                    recorded.transforms[0].position,
                    recorded.transforms[0].rotation
                );

            }

            StartCoroutine(Revel());

        }

    }

    /// <summary>
    /// Stops any current recording or playback, returning control to normal input.
    /// </summary>
    public void Stop() {

        mode = Mode.Neutral;
        CarController.externalController = false;

    }

    /// <summary>
    /// The main coroutine for reapplying recorded inputs frame-by-frame.
    /// </summary>
    private IEnumerator Replay() {

        // Step through each recorded frame's input data if we're in Play mode.
        for (int i = 0; i < recorded.inputs.Length && mode == Mode.Play; i++) {

            CarController.externalController = true;

            // Reassign each input field to the vehicle.
            CarController.throttleInput = recorded.inputs[i].throttleInput;
            CarController.brakeInput = recorded.inputs[i].brakeInput;
            CarController.steerInput = recorded.inputs[i].steerInput;
            CarController.handbrakeInput = recorded.inputs[i].handbrakeInput;
            CarController.clutchInput = recorded.inputs[i].clutchInput;
            CarController.boostInput = recorded.inputs[i].boostInput;
            CarController.fuelInput = recorded.inputs[i].fuelInput;
            CarController.direction = recorded.inputs[i].direction;
            CarController.canGoReverseNow = recorded.inputs[i].canGoReverse;
            CarController.currentGear = recorded.inputs[i].currentGear;
            CarController.changingGear = recorded.inputs[i].changingGear;

            // Reassign lights and indicators.
            CarController.indicatorsOn = recorded.inputs[i].indicatorsOn;
            CarController.lowBeamHeadLightsOn = recorded.inputs[i].lowBeamHeadLightsOn;
            CarController.highBeamHeadLightsOn = recorded.inputs[i].highBeamHeadLightsOn;

            // Wait for the next physics step before moving to the next frame's data.
            yield return new WaitForFixedUpdate();

        }

        // Once we've finished playback or exited early, revert to neutral mode and normal inputs.
        mode = Mode.Neutral;
        CarController.externalController = false;

    }

    /// <summary>
    /// A parallel coroutine that sets the vehicle's rigidbody velocity and angular velocity 
    /// for each frame of the recorded session.
    /// </summary>
    private IEnumerator Revel() {

        for (int i = 0; i < recorded.rigids.Length && mode == Mode.Play; i++) {

            CarController.Rigid.linearVelocity = recorded.rigids[i].velocity;
            CarController.Rigid.angularVelocity = recorded.rigids[i].angularVelocity;

            yield return new WaitForFixedUpdate();

        }

        mode = Mode.Neutral;
        CarController.externalController = false;

    }

    #endregion

    private void FixedUpdate() {

        if (!CarController)
            return;

        switch (mode) {

            case Mode.Neutral:
                // Do nothing special.
                break;

            case Mode.Play:
                // Ensure external controller remains active during playback.
                CarController.externalController = true;
                break;

            case Mode.Record:
                // Gather the current frame's data from the vehicle.
                Inputs.Add(new PlayerInput(
                    CarController.throttleInput,
                    CarController.brakeInput,
                    CarController.steerInput,
                    CarController.handbrakeInput,
                    CarController.clutchInput,
                    CarController.boostInput,
                    CarController.fuelInput,
                    CarController.direction,
                    CarController.canGoReverseNow,
                    CarController.currentGear,
                    CarController.changingGear,
                    CarController.indicatorsOn,
                    CarController.lowBeamHeadLightsOn,
                    CarController.highBeamHeadLightsOn
                ));

                Transforms.Add(new PlayerTransform(
                    CarController.transform.position,
                    CarController.transform.rotation
                ));

                Rigidbodies.Add(new PlayerRigidBody(
                    CarController.Rigid.linearVelocity,
                    CarController.Rigid.angularVelocity
                ));
                break;
        }

    }

}
