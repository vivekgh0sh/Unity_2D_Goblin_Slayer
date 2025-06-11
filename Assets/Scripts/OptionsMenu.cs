// OptionsMenu.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using System.Collections.Generic;
using System.Linq; // For Distinct and OrderBy in resolution setup
using TMPro;

public class OptionsMenu : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioMixer mainMixer;
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;

    // PlayerPrefs Keys
    public const string MASTER_VOL_KEY = "MasterVolume_PlayerPref"; // Made pref keys more distinct
    public const string MUSIC_VOL_KEY = "MusicVolume_PlayerPref";
    public const string SFX_VOL_KEY = "SFXVolume_PlayerPref";

    [Header("Graphics Settings")]
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;
    public const string RESOLUTION_INDEX_KEY = "ResolutionIndex_PlayerPref";
    public const string FULLSCREEN_KEY = "IsFullscreen_PlayerPref";

    private Resolution[] _availableResolutions;
    private List<Resolution> _filteredResolutions;
    private int _currentResolutionIndex = 0;

    [Header("Panel Management (Optional)")]
    public GameObject optionsPanel;
    public GameObject mainMenuPanel;

    void Start()
    {
        _filteredResolutions = new List<Resolution>(); // Initialize the list

        SetupVolumeSliders();
        SetupResolutionDropdownAndToggle();
        LoadSettings();
    }

    void SetupVolumeSliders()
    {
        masterVolumeSlider?.onValueChanged.AddListener(SetMasterVolume);
        musicVolumeSlider?.onValueChanged.AddListener(SetMusicVolume);
        sfxVolumeSlider?.onValueChanged.AddListener(SetSFXVolume);
    }

    void SetupResolutionDropdownAndToggle()
    {
        if (resolutionDropdown != null)
        {
            _availableResolutions = Screen.resolutions;
            resolutionDropdown.ClearOptions();

            // Filter for unique resolutions, prioritizing current refresh rate
            double currentMonitorRefreshRate = Screen.currentResolution.refreshRateRatio.value;
            _filteredResolutions = _availableResolutions
                .Where(res => System.Math.Abs(res.refreshRateRatio.value - currentMonitorRefreshRate) < 0.1) // Check current refresh rate
                .GroupBy(res => new { res.width, res.height }) // Group by width and height to get unique dimensions
                .Select(group => group.First()) // Select the first one (effectively distinct width/height at that rate)
                .OrderBy(res => res.width).ThenBy(res => res.height) // Order them
                .ToList();

            // Fallback if no resolutions matched current refresh rate (e.g. exclusive fullscreen mode reporting weirdly in editor)
            if (_filteredResolutions.Count == 0)
            {
                Debug.LogWarning("No resolutions matched current refresh rate. Falling back to all unique screen resolutions.");
                _filteredResolutions = _availableResolutions
                    .GroupBy(res => new { res.width, res.height, refreshRateValue = System.Math.Round(res.refreshRateRatio.value, 2) }) // Group by w, h, and rounded refresh rate
                    .Select(group => group.First())
                    .OrderBy(res => res.width).ThenBy(res => res.height).ThenBy(res => res.refreshRateRatio.value)
                    .ToList();
            }


            List<string> options = new List<string>();
            _currentResolutionIndex = -1; // Default to not found

            for (int i = 0; i < _filteredResolutions.Count; i++)
            {
                Resolution res = _filteredResolutions[i];
                // Ensure refreshRateRatio is valid before trying to get its value
                string refreshRateStr = "N/A";
                if (res.refreshRateRatio.denominator != 0)
                {
                    refreshRateStr = $"{System.Math.Round(res.refreshRateRatio.value, 0)} Hz";
                }
                else if (res.refreshRate > 0)
                { // Fallback for older Unity or specific platforms
                    refreshRateStr = $"{res.refreshRate} Hz";
                }

                string option = $"{res.width} x {res.height} @ {refreshRateStr}";
                options.Add(option);

                if (res.width == Screen.width && res.height == Screen.height)
                {
                    // Check refresh rate match more carefully for current selection
                    if (res.refreshRateRatio.denominator != 0 && Screen.currentResolution.refreshRateRatio.denominator != 0 &&
                        System.Math.Abs(res.refreshRateRatio.value - Screen.currentResolution.refreshRateRatio.value) < 0.1)
                    {
                        _currentResolutionIndex = i;
                    }
                    else if (res.refreshRate > 0 && Screen.currentResolution.refreshRate > 0 &&
                             res.refreshRate == Screen.currentResolution.refreshRate) // Fallback check
                    {
                        _currentResolutionIndex = i;
                    }
                }
            }

            // If current resolution was not perfectly matched, try to find a reasonable default
            if (_currentResolutionIndex == -1 && _filteredResolutions.Count > 0)
            {
                // Attempt to find based on width and height alone if refresh rate caused mismatch
                for (int i = 0; i < _filteredResolutions.Count; i++)
                {
                    if (_filteredResolutions[i].width == Screen.width && _filteredResolutions[i].height == Screen.height)
                    {
                        _currentResolutionIndex = i;
                        break;
                    }
                }
                // If still not found, default to the last (often highest) or first resolution in the sorted list
                if (_currentResolutionIndex == -1)
                {
                    _currentResolutionIndex = _filteredResolutions.Count - 1;
                }
            }


            resolutionDropdown.AddOptions(options);
            if (_currentResolutionIndex != -1 && _currentResolutionIndex < resolutionDropdown.options.Count)
            {
                resolutionDropdown.value = _currentResolutionIndex;
            }
            resolutionDropdown.RefreshShownValue();
            resolutionDropdown.onValueChanged.AddListener(SetResolutionFromDropdown);
        }
        else
        {
            Debug.LogWarning("Resolution Dropdown not assigned in OptionsMenu.");
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = Screen.fullScreen; // Or use Screen.fullScreenMode for more precise state
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }
        else
        {
            Debug.LogWarning("Fullscreen Toggle not assigned in OptionsMenu.");
        }
    }

    public void SetMasterVolume(float volume)
    {
        if (mainMixer == null) return;
        string paramName = "MasterVolume"; // Explicitly define here
        Debug.Log($"Attempting to set Mixer Parameter: '{paramName}' with linear volume: {volume}");
        mainMixer.SetFloat(paramName, Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20);
        PlayerPrefs.SetFloat(MASTER_VOL_KEY, volume);
        // Log current values of other params for comparison
        float musicMixerVol, sfxMixerVol;
        mainMixer.GetFloat("MusicVolume", out musicMixerVol);
        mainMixer.GetFloat("SFXVolume", out sfxMixerVol);
        Debug.Log($"After setting Master: MusicMixerVol_dB={musicMixerVol}, SFXMixerVol_dB={sfxMixerVol}");
    }

    public void SetMusicVolume(float volume)
    {
        if (mainMixer == null) return;
        string paramName = "MusicVolume"; // Explicitly define here
        Debug.Log($"Attempting to set Mixer Parameter: '{paramName}' with linear volume: {volume}");
        mainMixer.SetFloat(paramName, Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20);
        PlayerPrefs.SetFloat(MUSIC_VOL_KEY, volume);
        // Log current values of other params for comparison
        float masterMixerVol, sfxMixerVol;
        mainMixer.GetFloat("MasterVolume", out masterMixerVol);
        mainMixer.GetFloat("SFXVolume", out sfxMixerVol);
        Debug.Log($"After setting Music: MasterMixerVol_dB={masterMixerVol}, SFXMixerVol_dB={sfxMixerVol}");
    }

    public void SetSFXVolume(float volume)
    {
        if (mainMixer == null) return;
        string paramName = "SFXVolume"; // Explicitly define here
        Debug.Log($"Attempting to set Mixer Parameter: '{paramName}' with linear volume: {volume}");
        mainMixer.SetFloat(paramName, Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20);
        PlayerPrefs.SetFloat(SFX_VOL_KEY, volume);
        // Log current values of other params for comparison
        float masterMixerVol, musicMixerVol;
        mainMixer.GetFloat("MasterVolume", out masterMixerVol);
        mainMixer.GetFloat("MusicVolume", out musicMixerVol);
        Debug.Log($"After setting SFX: MasterMixerVol_dB={masterMixerVol}, MusicMixerVol_dB={musicMixerVol}");
    }

    public void SetResolutionFromDropdown(int resolutionIndex)
    {
        if (_filteredResolutions == null || resolutionIndex < 0 || resolutionIndex >= _filteredResolutions.Count) return;
        _currentResolutionIndex = resolutionIndex;
        // Resolution will be applied via ApplyGraphicsSettings button
    }

    public void SetFullscreen(bool isFullscreen)
    {
        // This directly changes the mode. ApplySelectedResolution will use this state if called.
        Screen.fullScreenMode = isFullscreen ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;
        // Consider FullScreenMode.FullScreenWindow for borderless option
        PlayerPrefs.SetInt(FULLSCREEN_KEY, isFullscreen ? 1 : 0);
        Debug.Log($"Fullscreen mode set to: {Screen.fullScreenMode}");
    }

    public void ApplyGraphicsSettings()
    {
        ApplySelectedResolution();
        Debug.Log("Graphics settings applied!");
    }

    private void ApplySelectedResolution()
    {
        if (_filteredResolutions == null || _currentResolutionIndex < 0 || _currentResolutionIndex >= _filteredResolutions.Count)
        {
            Debug.LogWarning("Cannot apply resolution: No valid resolution selected or available.");
            return;
        }

        Resolution resolution = _filteredResolutions[_currentResolutionIndex];
        FullScreenMode desiredFullScreenMode = fullscreenToggle?.isOn ?? Screen.fullScreen ?
                                               FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;
        // Alternative for borderless:
        // FullScreenMode desiredFullScreenMode = fullscreenToggle?.isOn ?? Screen.fullScreen ?
        //                                       FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        RefreshRate refreshRateStruct = new RefreshRate
        {
            numerator = (uint)resolution.refreshRateRatio.numerator,
            denominator = (uint)resolution.refreshRateRatio.denominator
        };

        // Fallback for refresh rate if numerator/denominator is zero (can happen with Screen.resolutions)
        if (refreshRateStruct.numerator == 0 || refreshRateStruct.denominator == 0)
        {
            Debug.LogWarning($"Resolution {resolution.width}x{resolution.height} had invalid refreshRateRatio num/den ({resolution.refreshRateRatio.numerator}/{resolution.refreshRateRatio.denominator}). Using current screen's rate or default.");
            if (Screen.currentResolution.refreshRateRatio.denominator != 0)
            {
                refreshRateStruct.numerator = (uint)Screen.currentResolution.refreshRateRatio.numerator;
                refreshRateStruct.denominator = (uint)Screen.currentResolution.refreshRateRatio.denominator;
            }
            else
            { // Absolute fallback
                refreshRateStruct.numerator = 60; refreshRateStruct.denominator = 1;
            }
        }


        Screen.SetResolution(resolution.width, resolution.height, desiredFullScreenMode, refreshRateStruct);
        PlayerPrefs.SetInt(RESOLUTION_INDEX_KEY, _currentResolutionIndex);
        // Fullscreen PlayerPref is saved in SetFullscreen method
        Debug.Log($"Resolution applied: {resolution.width}x{resolution.height} @ {System.Math.Round(refreshRateStruct.value, 0)}Hz, Mode: {desiredFullScreenMode}");
    }

    public void LoadSettings()
    {
        // Load Volume
        float masterVol = PlayerPrefs.GetFloat(MASTER_VOL_KEY, 0.75f);
        if (masterVolumeSlider != null) masterVolumeSlider.value = masterVol;
        SetMasterVolume(masterVol); // Apply to mixer

        float musicVol = PlayerPrefs.GetFloat(MUSIC_VOL_KEY, 0.75f);
        if (musicVolumeSlider != null) musicVolumeSlider.value = musicVol;
        SetMusicVolume(musicVol); // Apply to mixer

        float sfxVol = PlayerPrefs.GetFloat(SFX_VOL_KEY, 0.75f);
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = sfxVol;
        SetSFXVolume(sfxVol); // Apply to mixer

        // Load Graphics
        bool isFullscreen = PlayerPrefs.GetInt(FULLSCREEN_KEY, Screen.fullScreen ? 1 : 0) == 1;
        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = isFullscreen;
        }
        // Apply fullscreen state from prefs when loading, independent of Apply button for this part
        Screen.fullScreenMode = isFullscreen ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;


        // Resolution - Setup dropdown first, then load saved index
        // Note: SetupResolutionDropdownAndToggle() is called in Start() before LoadSettings().
        // It populates _filteredResolutions and sets an initial _currentResolutionIndex.
        if (resolutionDropdown != null && _filteredResolutions != null && _filteredResolutions.Count > 0)
        {
            int savedResolutionIndex = PlayerPrefs.GetInt(RESOLUTION_INDEX_KEY, -1);

            if (savedResolutionIndex >= 0 && savedResolutionIndex < _filteredResolutions.Count)
            {
                _currentResolutionIndex = savedResolutionIndex; // Use saved index if valid
            }
            // else _currentResolutionIndex remains what SetupResolutionDropdownAndToggle determined (current screen res)

            resolutionDropdown.value = _currentResolutionIndex;
            resolutionDropdown.RefreshShownValue();

            // DO NOT call ApplySelectedResolution() here automatically on load
            // unless you want resolution to change without hitting "Apply".
            // The visual dropdown will reflect the setting, user clicks "Apply" to enact it.
        }
        Debug.Log("Settings Loaded. MasterVol, MusicVol, SFXVol applied to mixer. Graphics UI updated.");
    }

    // --- Panel Management Example ---
    public void ToggleOptionsPanel()
    {
        bool isPanelAlreadyActive = optionsPanel != null && optionsPanel.activeSelf;
        bool shouldShowOptions = !isPanelAlreadyActive;

        optionsPanel?.SetActive(shouldShowOptions);
        mainMenuPanel?.SetActive(!shouldShowOptions);

        if (shouldShowOptions)
        {
            LoadSettings(); // Refresh UI values from PlayerPrefs when showing the panel
        }
    }

    public void CloseOptionsPanel()
    {
        optionsPanel?.SetActive(false);
        mainMenuPanel?.SetActive(true);
        // PlayerPrefs.Save(); // Not strictly necessary as SetFloat/SetInt save immediately.
    }
}