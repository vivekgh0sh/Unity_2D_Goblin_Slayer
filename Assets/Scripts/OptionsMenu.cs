// OptionsMenu.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using System.Collections.Generic;
using System.Linq;
using TMPro;

public class OptionsMenu : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioMixer mainMixer;
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;

    // --- Temporary storage for pending audio changes ---
    private float _pendingMasterVolume;
    private float _pendingMusicVolume;
    private float _pendingSFXVolume;

    public const string MASTER_VOL_KEY = "MasterVolume_PlayerPref";
    public const string MUSIC_VOL_KEY = "MusicVolume_PlayerPref";
    public const string SFX_VOL_KEY = "SFXVolume_PlayerPref";
    public const string MASTER_MIXER_PARAM = "MasterVolume"; // Exposed parameter name in AudioMixer
    public const string MUSIC_MIXER_PARAM = "MusicVolume";   // Exposed parameter name in AudioMixer
    public const string SFX_MIXER_PARAM = "SFXVolume";     // Exposed parameter name in AudioMixer

    [Header("Graphics Settings")]
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;

    // --- Temporary storage for pending graphics changes ---
    private int _pendingResolutionIndex;
    private bool _pendingIsFullscreen;

    public const string RESOLUTION_INDEX_KEY = "ResolutionIndex_PlayerPref";
    public const string FULLSCREEN_KEY = "IsFullscreen_PlayerPref";

    private Resolution[] _availableResolutions;
    private List<Resolution> _filteredResolutions;
    // Removed _currentResolutionIndex as pendingResolutionIndex will track UI choice

    [Header("Panel Management")]
    public GameObject optionsPanel;
    public GameObject mainMenuPanel;

    [Header("Buttons")]
    public Button applyButton; // Assign your "Apply" button here
    // Back button will also trigger Apply or Revert

    void Awake() // Changed to Awake to ensure pending values are set before Start might try to use them
    {
        _filteredResolutions = new List<Resolution>();
        // Initialize pending values with current/loaded settings to avoid null issues
        // LoadSettings will overwrite these if PlayerPrefs exist
        _pendingMasterVolume = PlayerPrefs.GetFloat(MASTER_VOL_KEY, 0.75f);
        _pendingMusicVolume = PlayerPrefs.GetFloat(MUSIC_VOL_KEY, 0.75f);
        _pendingSFXVolume = PlayerPrefs.GetFloat(SFX_VOL_KEY, 0.75f);
        _pendingIsFullscreen = PlayerPrefs.GetInt(FULLSCREEN_KEY, Screen.fullScreen ? 1 : 0) == 1;
        // _pendingResolutionIndex will be set during SetupResolutionDropdown or LoadSettings
    }

    void Start()
    {
        SetupEventlisteners();
        SetupResolutionDropdown(); // Renamed from SetupResolutionDropdownAndToggle
        LoadAndApplyInitialSettings(); // Loads and applies directly on start
    }

    void SetupEventlisteners()
    {
        masterVolumeSlider?.onValueChanged.AddListener(OnMasterVolumeSliderChanged);
        musicVolumeSlider?.onValueChanged.AddListener(OnMusicVolumeSliderChanged);
        sfxVolumeSlider?.onValueChanged.AddListener(OnSFXVolumeSliderChanged);
        resolutionDropdown?.onValueChanged.AddListener(OnResolutionDropdownChanged);
        fullscreenToggle?.onValueChanged.AddListener(OnFullscreenToggleChanged);
        applyButton?.onClick.AddListener(OnApplyButtonClicked);
    }

    void SetupResolutionDropdown()
    {
        if (resolutionDropdown == null)
        {
            Debug.LogWarning("Resolution Dropdown not assigned.");
            return;
        }

        _availableResolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();
        _filteredResolutions.Clear(); // Clear before repopulating

        double currentMonitorRefreshRate = Screen.currentResolution.refreshRateRatio.value;
        _filteredResolutions = _availableResolutions
            .Where(res => System.Math.Abs(res.refreshRateRatio.value - currentMonitorRefreshRate) < 0.1 || currentMonitorRefreshRate == 0) // Also allow if current rate is 0 (editor default)
            .GroupBy(res => new { res.width, res.height })
            .Select(group => group.OrderByDescending(r => r.refreshRateRatio.value).First()) // Get highest refresh rate for that WxH
            .OrderBy(res => res.width).ThenBy(res => res.height)
            .ToList();

        if (_filteredResolutions.Count == 0)
        {
            Debug.LogWarning("No resolutions matched current refresh rate. Falling back to all unique screen resolutions.");
            _filteredResolutions = _availableResolutions
                .GroupBy(res => new { res.width, res.height, refreshRateValue = System.Math.Round(res.refreshRateRatio.value, 2) })
                .Select(group => group.First())
                .OrderBy(res => res.width).ThenBy(res => res.height).ThenBy(res => res.refreshRateRatio.value)
                .ToList();
        }

        List<string> options = new List<string>();
        int activeResolutionIndex = -1;

        for (int i = 0; i < _filteredResolutions.Count; i++)
        {
            Resolution res = _filteredResolutions[i];
            string refreshRateStr = (res.refreshRateRatio.denominator != 0) ?
                                    $"{System.Math.Round(res.refreshRateRatio.value, 0)} Hz" :
                                    (res.refreshRate > 0 ? $"{res.refreshRate} Hz" : "N/A");
            options.Add($"{res.width} x {res.height} @ {refreshRateStr}");

            if (res.width == Screen.width && res.height == Screen.height &&
                (res.refreshRateRatio.denominator == 0 || Screen.currentResolution.refreshRateRatio.denominator == 0 || // Handle cases where ratio might be 0/0
                 System.Math.Abs(res.refreshRateRatio.value - Screen.currentResolution.refreshRateRatio.value) < 0.1))
            {
                activeResolutionIndex = i;
            }
        }

        if (activeResolutionIndex == -1 && _filteredResolutions.Count > 0) // Fallback if exact match not found
        {
            activeResolutionIndex = _filteredResolutions.Count - 1; // Default to highest available
            for (int i = 0; i < _filteredResolutions.Count; ++i)
            { // Or try to match width/height at least
                if (_filteredResolutions[i].width == Screen.width && _filteredResolutions[i].height == Screen.height)
                {
                    activeResolutionIndex = i;
                    break;
                }
            }
        }

        resolutionDropdown.AddOptions(options);
        if (activeResolutionIndex != -1 && activeResolutionIndex < resolutionDropdown.options.Count)
        {
            resolutionDropdown.value = activeResolutionIndex;
            _pendingResolutionIndex = activeResolutionIndex; // Initialize pending with current
        }
        resolutionDropdown.RefreshShownValue();
    }

    // --- UI Event Handlers (Update PENDING values) ---
    public void OnMasterVolumeSliderChanged(float volume)
    {
        _pendingMasterVolume = volume;
        // Optional: Preview sound if desired, but don't save or apply to mixer yet
        // PreviewMasterVolume(_pendingMasterVolume);
    }
    public void OnMusicVolumeSliderChanged(float volume)
    {
        _pendingMusicVolume = volume;
    }
    public void OnSFXVolumeSliderChanged(float volume)
    {
        _pendingSFXVolume = volume;
    }
    public void OnResolutionDropdownChanged(int resolutionIndex)
    {
        _pendingResolutionIndex = resolutionIndex;
    }
    public void OnFullscreenToggleChanged(bool isFullscreen)
    {
        _pendingIsFullscreen = isFullscreen;
    }

    // --- Apply Methods (Called by Apply Button or on Panel Close) ---
    private void ApplyAudioSettings()
    {
        if (mainMixer == null) return;
        Debug.Log($"Applying Audio - Master: {_pendingMasterVolume}, Music: {_pendingMusicVolume}, SFX: {_pendingSFXVolume}");

        mainMixer.SetFloat(MASTER_MIXER_PARAM, LinearToDecibels(_pendingMasterVolume));
        PlayerPrefs.SetFloat(MASTER_VOL_KEY, _pendingMasterVolume);

        mainMixer.SetFloat(MUSIC_MIXER_PARAM, LinearToDecibels(_pendingMusicVolume));
        PlayerPrefs.SetFloat(MUSIC_VOL_KEY, _pendingMusicVolume);

        mainMixer.SetFloat(SFX_MIXER_PARAM, LinearToDecibels(_pendingSFXVolume));
        PlayerPrefs.SetFloat(SFX_VOL_KEY, _pendingSFXVolume);
    }

    private void ApplyGraphicsSettingsInternal() // Renamed to avoid confusion with public ApplyGraphicsSettings
    {
        if (_filteredResolutions == null || _pendingResolutionIndex < 0 || _pendingResolutionIndex >= _filteredResolutions.Count)
        {
            Debug.LogWarning("Cannot apply resolution: No valid resolution selected or available.");
            return;
        }

        Resolution resolution = _filteredResolutions[_pendingResolutionIndex];
        FullScreenMode desiredFullScreenMode = _pendingIsFullscreen ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;
        // Consider: FullScreenMode.FullScreenWindow for borderless

        RefreshRate refreshRateStruct = new RefreshRate
        {
            numerator = (uint)resolution.refreshRateRatio.numerator,
            denominator = (uint)resolution.refreshRateRatio.denominator
        };
        if (refreshRateStruct.numerator == 0 || refreshRateStruct.denominator == 0) // Fallback
        {
            refreshRateStruct = Screen.currentResolution.refreshRateRatio.denominator != 0 ? Screen.currentResolution.refreshRateRatio : new RefreshRate { numerator = 60, denominator = 1 };
        }

        Screen.SetResolution(resolution.width, resolution.height, desiredFullScreenMode, refreshRateStruct);
        PlayerPrefs.SetInt(RESOLUTION_INDEX_KEY, _pendingResolutionIndex);
        PlayerPrefs.SetInt(FULLSCREEN_KEY, _pendingIsFullscreen ? 1 : 0);
        Debug.Log($"Graphics Applied: {resolution.width}x{resolution.height} @ {System.Math.Round(refreshRateStruct.value, 0)}Hz, Mode: {desiredFullScreenMode}");
    }

    // --- Button Click Handler ---
    public void OnApplyButtonClicked()
    {
        Debug.Log("Apply Button Clicked");
        ApplyAudioSettings();
        ApplyGraphicsSettingsInternal();
        PlayerPrefs.Save(); // Explicitly save all player prefs
    }

    // --- Load Settings ---
    public void LoadAndApplyInitialSettings() // Called once on Start
    {
        // Load Audio
        _pendingMasterVolume = PlayerPrefs.GetFloat(MASTER_VOL_KEY, 0.75f);
        _pendingMusicVolume = PlayerPrefs.GetFloat(MUSIC_VOL_KEY, 0.75f);
        _pendingSFXVolume = PlayerPrefs.GetFloat(SFX_VOL_KEY, 0.75f);
        ApplyAudioSettings(); // Apply directly to mixer on game start

        // Load Graphics
        _pendingIsFullscreen = PlayerPrefs.GetInt(FULLSCREEN_KEY, Screen.fullScreen ? 1 : 0) == 1;
        // Screen.fullScreenMode is set directly by ApplyGraphicsSettingsInternal

        if (resolutionDropdown != null && _filteredResolutions != null && _filteredResolutions.Count > 0)
        {
            _pendingResolutionIndex = PlayerPrefs.GetInt(RESOLUTION_INDEX_KEY, resolutionDropdown.value); // Default to current dropdown value if no pref
            if (_pendingResolutionIndex < 0 || _pendingResolutionIndex >= _filteredResolutions.Count)
            { // Ensure index is valid
                _pendingResolutionIndex = resolutionDropdown.options.Count > 0 ? resolutionDropdown.options.Count - 1 : 0;
            }
        }
        ApplyGraphicsSettingsInternal(); // Apply directly on game start

        // Update UI elements to reflect these initially applied settings
        UpdateUIToMatchPendingSettings();
        Debug.Log("Initial settings loaded and applied.");
    }

    private void LoadSettingsForUIRefresh() // Called when options panel opens
    {
        // Load values from PlayerPrefs into PENDING variables
        _pendingMasterVolume = PlayerPrefs.GetFloat(MASTER_VOL_KEY, 0.75f);
        _pendingMusicVolume = PlayerPrefs.GetFloat(MUSIC_VOL_KEY, 0.75f);
        _pendingSFXVolume = PlayerPrefs.GetFloat(SFX_VOL_KEY, 0.75f);
        _pendingIsFullscreen = PlayerPrefs.GetInt(FULLSCREEN_KEY, Screen.fullScreen ? 1 : 0) == 1;

        if (resolutionDropdown != null && _filteredResolutions != null && _filteredResolutions.Count > 0)
        {
            _pendingResolutionIndex = PlayerPrefs.GetInt(RESOLUTION_INDEX_KEY, resolutionDropdown.value);
            if (_pendingResolutionIndex < 0 || _pendingResolutionIndex >= _filteredResolutions.Count)
            {
                _pendingResolutionIndex = resolutionDropdown.options.Count > 0 ? resolutionDropdown.options.Count - 1 : 0;
            }
        }
        UpdateUIToMatchPendingSettings(); // Update UI to show these loaded values
    }


    private void UpdateUIToMatchPendingSettings()
    {
        if (masterVolumeSlider != null) masterVolumeSlider.value = _pendingMasterVolume;
        if (musicVolumeSlider != null) musicVolumeSlider.value = _pendingMusicVolume;
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = _pendingSFXVolume;
        if (fullscreenToggle != null) fullscreenToggle.isOn = _pendingIsFullscreen;
        if (resolutionDropdown != null && _pendingResolutionIndex >= 0 && _pendingResolutionIndex < resolutionDropdown.options.Count)
        {
            resolutionDropdown.value = _pendingResolutionIndex;
            resolutionDropdown.RefreshShownValue();
        }
    }


    // --- Panel Management ---
    public void ToggleOptionsPanel()
    {
        bool shouldShowOptions = optionsPanel != null && !optionsPanel.activeSelf;
        optionsPanel?.SetActive(shouldShowOptions);
        mainMenuPanel?.SetActive(!shouldShowOptions);

        if (shouldShowOptions)
        {
            LoadSettingsForUIRefresh(); // Load current settings and update UI elements
        }
    }

    public void CloseOptionsPanelAndApply() // Hook this to your "Back" button in options
    {
        OnApplyButtonClicked(); // Apply settings when closing
        optionsPanel?.SetActive(false);
        mainMenuPanel?.SetActive(true);
    }
    public void CloseOptionsPanelAndRevert() // Alternative for a "Cancel" or "Back without Saving"
    {
        // Don't apply pending changes. Reload settings from PlayerPrefs to revert UI.
        LoadSettingsForUIRefresh();
        optionsPanel?.SetActive(false);
        mainMenuPanel?.SetActive(true);
    }


    // --- Utility ---
    private float LinearToDecibels(float linear)
    {
        // Ensure linear is not zero or negative for Log10
        return Mathf.Log10(Mathf.Max(linear, 0.0001f)) * 20f;
    }
}