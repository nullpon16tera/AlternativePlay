using AlternativePlay.Models;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Zenject;

namespace AlternativePlay.UI
{
    [HotReload]
    public class AlternativePlayView : BSMLAutomaticViewController
    {
#pragma warning disable CS0649
        [Inject]
        private Configuration configuration;
        [Inject]
        private AlternativePlayMainFlowCoordinator mainFlowCoordinator;
#pragma warning restore CS0649

        private int deleteIndex; // Caches the index to be deleted for after the Delete Modal is done
        private int renameIndex;
        private PlayModeSettings renameSettings;
        private bool renameKeyboardConfigured;

        /// <summary>
        /// Reloads the table with the latest configuration data
        /// </summary>
        /// <param name="index">Optional parameter for the row to scroll the table to.</param>
        public void RefreshConfigurations(int index = -1)
        {
            // Convert configuration settings to the class used for the list
            var list = this.configuration.ConfigurationData.PlayModeSettings
                .Select((settings, i) => new PlayModeSelectOption(this.configuration.ConfigurationData, i, this.ShowDeleteModal, this.ShowRenameModal))
                .ToList();

            this.SelectModeList.TableView.ClearSelection();
            this.SelectModeList.Data.Clear();
            this.SelectModeList.Data = list.Cast<object>().ToList();
            this.SelectModeList.TableView.ReloadData();

            if (index != -1)
            {
                this.SelectModeList.TableView.ScrollToCellWithIdx(index, TableView.ScrollPositionType.End, false);
            }
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            if (firstActivation)
            {
                this.RefreshConfigurations();
            }
        }

        /// <summary>
        /// Shows the Delete confirmation modal.  Deletion happens after the user selects OK.
        /// </summary>
        private void ShowDeleteModal(int index)
        {
            this.deleteIndex = index;
            this.DeleteModal.Show(true);
        }

        private void ShowRenameModal(int index)
        {
            PlayModeSettings playModeSetting = this.configuration.GetPlayModeSetting(index);
            if (playModeSetting == null)
            {
                return;
            }

            this.renameIndex = index;
            this.renameSettings = playModeSetting;
            KEYBOARD keyboard = this.RenameKeyboard.Keyboard;
            if (!this.renameKeyboardConfigured)
            {
                keyboard.AddKeys("@-30,11 [PASTE]/20 [CANCEL]/20", 0.5f);
                keyboard.SetAction("PASTE", _ =>
                {
                    this.RenameKeyboard.SetText(PlayModeSettings.NormalizePresetName(GUIUtility.systemCopyBuffer) ?? string.Empty);
                });
                keyboard.SetAction("CANCEL", _ =>
                {
                    this.renameSettings = null;
                    this.RenameKeyboard.ModalView.Hide(true);
                });
                keyboard.SetAction("⬅", _ =>
                {
                    string text = keyboard.KeyboardText.text ?? string.Empty;
                    int[] combining = StringInfo.ParseCombiningCharacters(text);
                    this.RenameKeyboard.SetText(combining.Length == 0 ? string.Empty : text.Substring(0, combining[combining.Length - 1]));
                });
                keyboard.KeyboardText.richText = false;
                this.renameKeyboardConfigured = true;
            }

            this.RenameKeyboard.ModalView.Show(true);
            this.RenameKeyboard.SetText(PlayModeSettings.NormalizePresetName(playModeSetting.PresetName) ?? string.Empty);
        }

        [UIAction("OnRenameConfirmed")]
        public void OnRenameConfirmed(string name)
        {
            if (this.configuration.RenamePlayModeSetting(this.renameIndex, this.renameSettings, name))
            {
                this.RefreshConfigurations(this.renameIndex);
            }

            this.renameSettings = null;
        }

        [UIComponent(nameof(SelectModeList))]
        public readonly CustomCellListTableData SelectModeList;

        [UIComponent("RenameKeyboard")]
        public ModalKeyboard RenameKeyboard;

        [UIAction(nameof(OnModeClicked))]
        public void OnModeClicked(TableView _, PlayModeSelectOption selected)
        {
            var playModeSettings = this.configuration.GetPlayModeSetting(selected.Index);
            if (playModeSettings == null)
            {
                // Do nothing as this is an error
                return;
            }

            this.mainFlowCoordinator.ShowPlayModeSelect(playModeSettings, selected.Index);
        }

        [UIAction(nameof(OnAddNewConfiguration))]
        public void OnAddNewConfiguration()
        {
            // Add a new setting to the bottom of the list
            this.configuration.AddPlayModeSetting();

            int index = this.configuration.ConfigurationData.PlayModeSettings.Count - 1;
            this.RefreshConfigurations(index);
        }

        [UIComponent("DeleteModal")]
        public ModalView DeleteModal;

        [UIAction("OnOKClicked")]
        public void OnOKClicked()
        {
            // Delete the playmode setting at the saved index
            this.configuration.DeletePlayModeSetting(this.deleteIndex);
            int scrollToIndex = this.deleteIndex >= this.configuration.ConfigurationData.PlayModeSettings.Count
                ? this.configuration.ConfigurationData.PlayModeSettings.Count - 1
                : this.deleteIndex;
            this.RefreshConfigurations(scrollToIndex);

            this.DeleteModal.Hide(true);
        }

        [UIAction("OnCancelClicked")]
        public void OnCancelClicked()
        {
            this.DeleteModal.Hide(true);
        }
    }

}
