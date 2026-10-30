using System.Collections;
using UnityEngine;

public enum FeedbackKind { Neutral, Success, Error }

/// <summary>
/// Entry point. Add this component to an empty GameObject in a scene that already has
/// an XR rig. It builds the target object, the editable object and the VR control panel
/// in front of the user at startup.
/// </summary>
public class MatchGameController : MonoBehaviour
{
    [Header("Validation")]
    [Tooltip("Max angular error (degrees) still accepted as the correct rotation.")]
    public float rotationToleranceDegrees = 20f;
    [Tooltip("If true, an incorrect answer also tells how many properties differ (never which).")]
    public bool revealMismatchCount = false;

    [Header("Behaviour")]
    public float rotationStepDegrees = 15f;
    public ObjectSpec initialSpec = new ObjectSpec(ShapeType.Cube, ColorType.Red, SizeType.Medium, Vector3.zero);

    [Header("Layout (relative to the user's head at startup)")]
    public float startDelaySeconds = 0.5f;
    public float distanceFromUser = 1.3f;
    public float panelHeightOffset = -0.42f;
    public float objectHeightOffset = 0.3f;
    public float objectSideOffset = 0.5f;

    public ObjectSpec Current => current;
    public int Attempts { get; private set; }
    public bool Solved { get; private set; }
    public string FeedbackMessage { get; private set; } = "";
    public FeedbackKind FeedbackKind { get; private set; } = FeedbackKind.Neutral;

    /// <summary>Raised whenever anything the UI displays has changed.</summary>
    public event System.Action Changed;

    ObjectSpec current;
    ObjectSpec target;
    ShapeView playerView;
    ShapeView targetView;
    bool initialized;

    const string NeutralMessage = "Adjust your object to match the target, then press Confirm.";

    IEnumerator Start()
    {
        // Give XR tracking a moment so the head pose is valid before placing content.
        yield return new WaitForSeconds(startDelaySeconds);

        Camera cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        Vector3 head = cam != null ? cam.transform.position : new Vector3(0f, 1.6f, 0f);
        Vector3 forward = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up) : Vector3.forward;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 center = head + forward * distanceFromUser;
        Quaternion facing = Quaternion.LookRotation(forward, Vector3.up);

        Vector3 targetPos = center + Vector3.up * objectHeightOffset - right * objectSideOffset;
        Vector3 playerPos = center + Vector3.up * objectHeightOffset + right * objectSideOffset;

        targetView = ShapeView.Create("TargetObject", targetPos);
        playerView = ShapeView.Create("PlayerObject", playerPos);

        var ui = gameObject.AddComponent<MatchUIBuilder>();
        ui.Build(this, center + Vector3.up * panelHeightOffset, facing);
        ui.CreateLabel("TARGET", targetPos + Vector3.up * 0.4f, facing);
        ui.CreateLabel("YOUR OBJECT", playerPos + Vector3.up * 0.4f, facing);

        initialized = true;
        NewRound();
    }

    // ---------------------------------------------------------------- Player actions

    public void SetShape(ShapeType shape)
    {
        if (!CanEdit()) return;
        current.shape = shape;
        OnEdited();
    }

    public void SetColor(ColorType color)
    {
        if (!CanEdit()) return;
        current.color = color;
        OnEdited();
    }

    public void SetSize(SizeType size)
    {
        if (!CanEdit()) return;
        current.size = size;
        OnEdited();
    }

    /// <param name="axis">0 = X, 1 = Y, 2 = Z</param>
    /// <param name="direction">+1 or -1</param>
    public void RotateAxis(int axis, float direction)
    {
        if (!CanEdit()) return;
        current.eulerDegrees[axis] = Mathf.Repeat(current.eulerDegrees[axis] + direction * rotationStepDegrees, 360f);
        OnEdited();
    }

    public void ResetObject()
    {
        if (!CanEdit()) return;
        current = initialSpec;
        playerView.Apply(current);
        SetFeedback(FeedbackKind.Neutral, "Object reset to its original state.");
    }

    /// <summary>Validates the answer. Once solved, the same button starts a new round.</summary>
    public void Confirm()
    {
        if (!initialized) return;

        if (Solved)
        {
            NewRound();
            return;
        }

        Attempts++;
        MatchResult result = MatchValidator.Evaluate(current, target, rotationToleranceDegrees);

        if (result.IsCorrect)
        {
            Solved = true;
            SetFeedback(FeedbackKind.Success, "Correct! Your object matches the target.");
        }
        else
        {
            string message = "Not quite. Something still doesn't match - keep adjusting and try again.";
            if (revealMismatchCount) message += " (" + result.MismatchCount + " of 4 properties differ)";
            SetFeedback(FeedbackKind.Error, message);
        }
    }

    public void NewRound()
    {
        target = RandomTarget();
        current = initialSpec;
        Solved = false;
        Attempts = 0;

        targetView.Apply(target, true);
        playerView.Apply(current, true);
        SetFeedback(FeedbackKind.Neutral, NeutralMessage);
    }

    // ---------------------------------------------------------------- Internals

    bool CanEdit() => initialized && !Solved;

    void OnEdited()
    {
        playerView.Apply(current);
        SetFeedback(FeedbackKind.Neutral, NeutralMessage);
    }

    void SetFeedback(FeedbackKind kind, string message)
    {
        FeedbackKind = kind;
        FeedbackMessage = message;
        Changed?.Invoke();
    }

    ObjectSpec RandomTarget()
    {
        // Retry so the target is never already satisfied by the starting object.
        for (int i = 0; i < 50; i++)
        {
            var candidate = new ObjectSpec(
                (ShapeType)UnityEngine.Random.Range(0, 3),
                (ColorType)UnityEngine.Random.Range(0, 3),
                (SizeType)UnityEngine.Random.Range(0, 3),
                new Vector3(RandomAngle(), RandomAngle(), RandomAngle()));

            if (!MatchValidator.Evaluate(initialSpec, candidate, rotationToleranceDegrees).IsCorrect)
                return candidate;
        }
        return new ObjectSpec(ShapeType.Cylinder, ColorType.Blue, SizeType.Large, new Vector3(90f, 0f, 0f));
    }

    // Multiples of 45 degrees, so every target rotation is reachable with the 15 degree steps.
    static float RandomAngle() => 45f * UnityEngine.Random.Range(0, 8);
}
