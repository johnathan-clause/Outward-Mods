﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace CombatHUD_2
{
    public class TargetManager : MonoBehaviour
    {
        public int Split_ID;

        private Character m_LinkedCharacter;
        private string m_lastTargetUID;

        // Target HUD stuff
        private GameObject m_TargetHUDHolder; // Each SplitPlayer gets one of these.
        private Text m_targetHealthText; // holder for showing enemy's health text
        private GameObject m_StatusHolder; // holds all the enemy status icon and text holders

        // Infobox Stuff
        private GameObject m_infoboxHolder; // each SplitPlayer gets one of these.
        private Text m_infoboxName; // targeted enemy's name
        private Text m_infoboxHealth; // active health / max health
        private Text m_infoboxImpact; // impact res
        private List<Text> m_infoboxDamageTexts = new List<Text>(); // [0] -> [5], enemy resistances, uses (DamageType.Types)int
        private Text m_infoboxNoImmuneText; // the "none" text
        private Image m_infoboxBurningSprite; // Burning sprite
        private Image m_infoboxBleedingSprite; // Bleeding sprite
        private Image m_infoboxPoisonSprite; // Poison sprite

        #region Awake (setup global vars from the prefab asset)
        internal void Awake()
        {
            // Setup TargetHUD
            m_TargetHUDHolder = this.transform.Find("TargetHUD_Holder").gameObject;
            m_targetHealthText = m_TargetHUDHolder.transform.Find("text_Health").GetComponent<Text>();
            m_StatusHolder = m_TargetHUDHolder.transform.Find("StatusEffects_Holder").gameObject;
            foreach (Transform child in m_StatusHolder.transform)
            {
                child.gameObject.SetActive(false);
            }

            // Setup Infobox
            m_infoboxHolder = this.transform.Find("InfoboxHolder").gameObject;

            var texts = m_infoboxHolder.transform.Find("Texts");

            m_infoboxName   = texts.Find("text_Name").GetComponent<Text>();
            m_infoboxHealth = texts.Find("text_Health").GetComponent<Text>();
            m_infoboxImpact = texts.Find("text_Impact").GetComponent<Text>();

            var damageTexts = texts.Find("DamageTexts");
            m_infoboxDamageTexts.Add(damageTexts.Find("text_Phys").GetComponent<Text>());
            m_infoboxDamageTexts.Add(damageTexts.Find("text_Ethereal").GetComponent<Text>());
            m_infoboxDamageTexts.Add(damageTexts.Find("text_Decay").GetComponent<Text>());
            m_infoboxDamageTexts.Add(damageTexts.Find("text_Lightning").GetComponent<Text>());
            m_infoboxDamageTexts.Add(damageTexts.Find("text_Frost").GetComponent<Text>());
            m_infoboxDamageTexts.Add(damageTexts.Find("text_Fire").GetComponent<Text>());

            var statusIcons  = texts.Find("StatusIcons");
            m_infoboxBleedingSprite = statusIcons.Find("Bleeding").GetComponent<Image>();
            m_infoboxBurningSprite  = statusIcons.Find("Burning").GetComponent<Image>();
            m_infoboxPoisonSprite   = statusIcons.Find("Poison").GetComponent<Image>();
            m_infoboxNoImmuneText   = statusIcons.Find("None").GetComponent<Text>();
        }
        #endregion

        internal void Update()
        {
            if (m_LinkedCharacter == null)
            {
                if (SplitScreenManager.Instance.LocalPlayerCount > Split_ID && SplitScreenManager.Instance.LocalPlayers[Split_ID].AssignedCharacter)
                {
                    m_LinkedCharacter = SplitScreenManager.Instance.LocalPlayers[Split_ID].AssignedCharacter;
                }
                else
                {
                    DisableHolders();   
                }
            }
            else
            {
                if (m_LinkedCharacter.TargetingSystem.Locked)
                {
                    UpdateTarget();
                }
                else
                {
                    DisableHolders();
                }
            }
        }

        private void UpdateTarget()
        {
            var target = m_LinkedCharacter.TargetingSystem.LockedCharacter;

            if (target.UID != m_lastTargetUID)
            {
                m_lastTargetUID = target.UID;
                UpdateOnTargetChange();
            }

            UpdateInfobox(target);
            UpdateTargetHUD(target);

            EnableHolders();
        }

        private void UpdateInfobox(Character target)
        {
            m_infoboxHealth.text = Math.Round(target.Stats.CurrentHealth) + " / " + Math.Round(target.Stats.ActiveMaxHealth);
            m_infoboxImpact.text = Math.Round(target.Stats.GetImpactResistance()).ToString();

            for (int i = 0; i < 6; i++)
            {
                float value = target.Stats.GetDamageResistance((DamageType.Types)i) * 100f;
                m_infoboxDamageTexts[i].text = Math.Round(value).ToString();

                if (value > 0)
                {
                    m_infoboxDamageTexts[i].color = new Color(0.3f, 1.0f, 0.3f);
                }
                else if (value < 0)
                {
                    m_infoboxDamageTexts[i].color = new Color(1.0f, 0.4f, 0.4f);
                }
                else
                {
                    m_infoboxDamageTexts[i].color = new Color(0.3f, 0.3f, 0.3f);
                }
            }
        }

        private void UpdateTargetHUD(Character target)
        {
            // update health text
            m_targetHealthText.text = Math.Round(target.Stats.CurrentHealth) + " / " + Math.Round(target.Stats.ActiveMaxHealth);
            m_targetHealthText.rectTransform.position = RectTransformUtility.WorldToScreenPoint(m_LinkedCharacter.CharacterCamera.CameraScript, target.UIBarPosition);
            m_targetHealthText.rectTransform.position += Vector3.up * HUDManager.RelativeOffset(5f, true);

            // update status icons
            float offset = 0f;
            var barPos = RectTransformUtility.WorldToScreenPoint(m_LinkedCharacter.CharacterCamera.CameraScript, target.UIBarPosition);
            var pos = barPos + new Vector2(HUDManager.RelativeOffset(175f), 0);
            for (int i = 0; i < m_StatusHolder.transform.childCount; i++)
            {
                var obj = m_StatusHolder.transform.GetChild(i).gameObject;
                string identifier = obj.name;
                var status = target.StatusEffectMngr.Statuses.Find(x => x.IdentifierName == identifier);

                if (!status)
                {
                    obj.SetActive(false);
                }
                else
                {
                    var parentRect = obj.GetComponent<RectTransform>();
                    parentRect.position = new Vector3(pos.x, pos.y + offset);

                    var text = parentRect.transform.Find("Text").GetComponent<Text>();
                    TimeSpan t = TimeSpan.FromSeconds(status.RemainingLifespan);
                    var s = string.Format("{0}:{1}", t.Minutes, t.Seconds.ToString("00"));
                    text.text = s;
                    text.color = Color.white;

                    if (!obj.activeSelf)
                    {
                        obj.SetActive(true);
                    }

                    offset -= HUDManager.RelativeOffset(20f, true);
                }
            }

            // buildups
            var m_statusBuildup = At.GetValue(typeof(StatusEffectManager), target.StatusEffectMngr, "m_statusBuildUp");
            IDictionary dict = m_statusBuildup as IDictionary;
            FieldInfo buildupField = m_statusBuildup.GetType().GetGenericArguments()[1].GetField("BuildUp");

            foreach (string name in dict.Keys)
            {
                //GameObject holder = null;
                if (m_StatusHolder.transform.Find(name) is Transform t)
                {
                    var holder = t.gameObject;
                    if (holder.activeSelf)
                    {
                        // status is already active (ie. its 100%)
                        continue;
                    }
                    else
                    {
                        float value = (float)buildupField.GetValue(dict[name]);

                        if (value > 0 && value < 100)
                        {
                            var parentRect = holder.GetComponent<RectTransform>();
                            parentRect.position = new Vector3(pos.x, pos.y + offset);
                            offset -= HUDManager.RelativeOffset(20f, true);

                            var text = holder.GetComponentInChildren<Text>();
                            text.text = Math.Round(value) + "%";
                            text.color = new Color(1.0f, 0.5f, 0.5f, value * 0.01f + 0.25f);

                            if (!holder.activeSelf)
                            {
                                holder.SetActive(true);
                            }
                        }
                    }
                }
            }
        }

        private void UpdateOnTargetChange()
        {
            var target = m_LinkedCharacter.TargetingSystem.LockedCharacter;

            m_infoboxName.text = target.Name;

            // only update status immunities when we change targets.
            List<string> immunityTags = new List<string>();

            var statusNaturalImmunities = At.GetValue(typeof(CharacterStats), target.Stats, "m_statusEffectsNaturalImmunity") as TagSourceSelector[];
            foreach (TagSourceSelector tagSelector in statusNaturalImmunities)
                immunityTags.Add(tagSelector.Tag.TagName);

            var statusImmunities = At.GetValue(typeof(CharacterStats), target.Stats, "m_statusEffectsImmunity") as Dictionary<Tag, List<string>>;
            foreach (KeyValuePair<Tag, List<string>> entry in statusImmunities)
            {
                if (entry.Value.Count > 0)
                    immunityTags.Add(entry.Key.TagName);
            }

            if (immunityTags.Count > 0)
            {
                m_infoboxNoImmuneText.gameObject.SetActive(false);
                float offset = 0f;
                var pos = m_infoboxNoImmuneText.rectTransform.transform.position;

                if (immunityTags.Contains("Bleeding"))
                {
                    m_infoboxBleedingSprite.gameObject.SetActive(true);
                    m_infoboxBleedingSprite.rectTransform.position = new Vector3(pos.x, pos.y - 2f, 0);
                    offset += HUDManager.RelativeOffset(22f);
                }
                else
                {
                    m_infoboxBleedingSprite.gameObject.SetActive(false);
                }
                if (immunityTags.Contains("Burning"))
                {
                    m_infoboxBurningSprite.gameObject.SetActive(true);
                    m_infoboxBurningSprite.rectTransform.position = new Vector3(pos.x + offset, pos.y - 2f, 0);
                    offset += HUDManager.RelativeOffset(22f);
                }
                else
                {
                    m_infoboxBurningSprite.gameObject.SetActive(false);
                }
                if (immunityTags.Contains("Poison"))
                {
                    m_infoboxPoisonSprite.gameObject.SetActive(true);
                    m_infoboxPoisonSprite.rectTransform.position = new Vector3(pos.x + offset, pos.y - 2f, 0);
                }
                else
                {
                    m_infoboxPoisonSprite.gameObject.SetActive(false);
                }
            }
            else
            {
                m_infoboxNoImmuneText.gameObject.SetActive(true);

                m_infoboxBurningSprite.gameObject.SetActive(false);
                m_infoboxBleedingSprite.gameObject.SetActive(false);
                m_infoboxPoisonSprite.gameObject.SetActive(false);
            }
        }

        private void EnableHolders()
        {
            // todo needs Settings
            if (!m_TargetHUDHolder.activeSelf)
            {
                m_TargetHUDHolder.SetActive(true);
            }
            if (!m_infoboxHolder.activeSelf)
            {
                m_infoboxHolder.SetActive(true);
            }
        }

        private void DisableHolders()
        {
            if (m_TargetHUDHolder.activeSelf)
            {
                m_TargetHUDHolder.SetActive(false);
            }

            if (m_infoboxHolder.activeSelf)
            {
                m_infoboxHolder.SetActive(false);
            }
        }
    }
}
