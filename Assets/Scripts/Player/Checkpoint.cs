// Checkpoint.cs
using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    // Static variables hold the state of the last activated checkpoint for all instances
    public static Vector3 LastCheckpointPosition { get; private set; }
    public static bool _initialCheckpointSet_InternalUseOnly { get; private set; } = false; // Internal flag for Player.cs
    private static Checkpoint _currentActiveCheckpointVisualInstance; // To manage visuals of the active one

    // Instance variables for visual feedback (optional)
    private SpriteRenderer _spriteRenderer;
    public Sprite activeSprite;         // Assign in Inspector: Sprite when this checkpoint is THE active one
    public Sprite inactiveSprite;       // Assign in Inspector: Sprite when not the active one but passed
    public Sprite pristineSprite;       // Assign in Inspector: Sprite before player ever reaches it (optional)

    private bool _hasBeenReached = false; // Instance specific: has this particular checkpoint been reached?

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();

        // Set the very first checkpoint based on tag and if no initial has been set yet
        if (!_initialCheckpointSet_InternalUseOnly && gameObject.CompareTag("InitialCheckpoint"))
        {
            LastCheckpointPosition = transform.position;
            _initialCheckpointSet_InternalUseOnly = true; // Mark that an initial checkpoint is now defined
            _currentActiveCheckpointVisualInstance = this;
            _hasBeenReached = true; // The initial checkpoint is considered "reached"
            UpdateVisualState();
            Debug.Log($"Initial checkpoint registered at: {transform.position} by {gameObject.name}");
        }
        else
        {
            UpdateVisualState(); // Set initial visual state (likely pristine or inactive)
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // If this checkpoint is new OR it's an already reached checkpoint being re-activated as the latest
            if (!_hasBeenReached || LastCheckpointPosition != transform.position)
            {
                Debug.Log($"Player reached checkpoint: {gameObject.name} at {transform.position}");
                LastCheckpointPosition = transform.position; // Update the global last checkpoint position

                // Deactivate visuals of the previously active checkpoint
                if (_currentActiveCheckpointVisualInstance != null && _currentActiveCheckpointVisualInstance != this)
                {
                    _currentActiveCheckpointVisualInstance._hasBeenReached = true; // Mark old one as reached but not latest
                    _currentActiveCheckpointVisualInstance.UpdateVisualState();
                }

                _currentActiveCheckpointVisualInstance = this; // This is now the latest active checkpoint
                _hasBeenReached = true; // Mark this specific checkpoint as reached
                UpdateVisualState();    // Update its visuals to active

                Player player = other.GetComponent<Player>();
                if (player != null)
                {
                    player.PlayCheckpointReachedSound(); // Tell player to play sound
                }
            }
        }
    }

    // Updates the visual appearance of THIS checkpoint instance
    public void UpdateVisualState()
    {
        if (_spriteRenderer == null || activeSprite == null || inactiveSprite == null) return; // Need sprites

        if (this == _currentActiveCheckpointVisualInstance)
        {
            _spriteRenderer.sprite = activeSprite; // This is THE current spawn point
        }
        else if (_hasBeenReached)
        {
            _spriteRenderer.sprite = inactiveSprite; // Reached, but not the latest spawn point
        }
        else if (pristineSprite != null) // Optional: If you have a "not yet reached" sprite
        {
            _spriteRenderer.sprite = pristineSprite;
        }
        else // Default to inactive if no pristine sprite
        {
            _spriteRenderer.sprite = inactiveSprite;
        }
    }

    // Static method to reset the checkpoint system (e.g., on game start or new level)
    public static void ResetToInitialCheckpoint(Vector3 initialPlayerPosition)
    {
        LastCheckpointPosition = initialPlayerPosition; // Default to player start if no tagged checkpoint found early
        _initialCheckpointSet_InternalUseOnly = false; // Allow Awake to re-evaluate for "InitialCheckpoint" tag

        // Attempt to find and visually reset the "InitialCheckpoint" tagged object if it exists
        // and deactivate any other potentially "active" checkpoint visuals from a previous session.
        if (_currentActiveCheckpointVisualInstance != null)
        {
            _currentActiveCheckpointVisualInstance._hasBeenReached = false; // Reset its reached state
            _currentActiveCheckpointVisualInstance.UpdateVisualState(); // Should go to pristine/inactive
            _currentActiveCheckpointVisualInstance = null;
        }


        Checkpoint[] allCheckpoints = FindObjectsOfType<Checkpoint>();
        bool foundInitial = false;
        foreach (Checkpoint cp in allCheckpoints)
        {
            cp._hasBeenReached = false; // Reset reached state for all
            if (cp.CompareTag("InitialCheckpoint"))
            {
                LastCheckpointPosition = cp.transform.position; // Prioritize tagged initial checkpoint
                _initialCheckpointSet_InternalUseOnly = true;
                _currentActiveCheckpointVisualInstance = cp;
                cp._hasBeenReached = true;
                foundInitial = true;
                // cp.UpdateVisualState(); // Awake will handle initial visual for this one
            }
            // cp.UpdateVisualState(); // Let Awake handle most initial states to avoid conflicts
        }
        if (foundInitial && _currentActiveCheckpointVisualInstance != null)
        {
            _currentActiveCheckpointVisualInstance.UpdateVisualState(); // Ensure the found initial one is active
        }


        Debug.Log($"Checkpoint system reset. LastCheckpointPosition set to: {LastCheckpointPosition}");
    }
}