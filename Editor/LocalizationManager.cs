using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace MeshUVMaskGenerator
{
    public enum SupportedLanguage
    {
        Japanese,
        English,
        Korean
    }

    public static class LocalizationManager
    {
        private const string LANGUAGE_PREF_KEY = "MeshUVMaskGenerator_Language";
        private static SupportedLanguage currentLanguage = SupportedLanguage.Japanese;
        private static bool isInitialized = false;

        public static event Action OnLanguageChanged;

        public static SupportedLanguage CurrentLanguage
        {
            get
            {
                if (!isInitialized)
                {
                    Initialize();
                }
                return currentLanguage;
            }
            set
            {
                if (currentLanguage != value)
                {
                    currentLanguage = value;
                    SaveLanguagePreference();
                    OnLanguageChanged?.Invoke();
                }
            }
        }

        private static void Initialize()
        {
            // EditorPrefsから言語設定を読み込み
            string savedLanguage = EditorPrefs.GetString(LANGUAGE_PREF_KEY, "Japanese");
            
            if (Enum.TryParse<SupportedLanguage>(savedLanguage, out SupportedLanguage language))
            {
                currentLanguage = language;
            }
            else
            {
                currentLanguage = SupportedLanguage.Japanese;
            }

            isInitialized = true;
        }

        private static void SaveLanguagePreference()
        {
            EditorPrefs.SetString(LANGUAGE_PREF_KEY, currentLanguage.ToString());
        }

        public static string GetText(string key)
        {
            if (!isInitialized)
            {
                Initialize();
            }

            return LocalizationResources.GetText(key, currentLanguage);
        }

        public static string[] GetLanguageDisplayNames()
        {
            return new string[]
            {
                "日本語",
                "English",
                "한국어"
            };
        }

        public static int GetLanguageIndex()
        {
            return (int)CurrentLanguage;
        }

        public static void SetLanguageByIndex(int index)
        {
            if (index >= 0 && index < Enum.GetValues(typeof(SupportedLanguage)).Length)
            {
                CurrentLanguage = (SupportedLanguage)index;
            }
        }
    }
}