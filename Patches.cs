using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Graphics.GUI;
using SpaceEngineers.Game.GUI;
using VRage;
using VRageMath;

namespace Respawn_Menu_Improvements
{
    public class Patches
    {
        public static MyGuiControlSearchBox searchBox = null;
        public static MyGuiControlTable respawnsTable = null;
        public static List<MyGuiControlTable.Row> allRows = null;

        [HarmonyPatch(typeof(MyGuiScreenMedicals), "RecreateControlsRespawn")]
        public class RecreateControlsRespawnPatch
        {
            public static void Postfix(MyGuiScreenMedicals __instance)
            {
                searchBox = new MyGuiControlSearchBox(new Vector2(0f, 0.244f), new Vector2(0.3593f, 0f));
                searchBox.OnTextChanged += OnSearchBoxTextChanged;

                __instance.Controls.Add(searchBox);

                //__instance.Controls.Add(new MyGuiControlButton(new Vector2(0f, 0f), text: new StringBuilder("test"))
                //{
                //    BorderEnabled = false,
                //    BorderSize = 0,
                //    BorderHighlightEnabled = false,
                //    BorderColor = Vector4.Zero,
                //});
            }
        }

        public static void OnSearchBoxTextChanged(string newText)
        {
            if (allRows != null && respawnsTable != null)
            {
                if (!String.IsNullOrWhiteSpace(newText))
                {
                    for (int i = 0; i < allRows.Count; i++)
                    {
                        if (respawnsTable.Rows.Contains(allRows[i]))
                        {
                            respawnsTable.Remove(allRows[i]);
                        }
                        if (allRows[i].GetCell(0).Text.ToString().Contains(newText, StringComparison.OrdinalIgnoreCase))
                        {
                            respawnsTable.Insert(respawnsTable.Rows.Count, allRows[i]);
                        }
                    }
                }
                else 
                {
                    for (int i = 0; i < allRows.Count; i++)
                    {
                        if (respawnsTable.Rows.Contains(allRows[i]))
                        {
                            respawnsTable.Remove(allRows[i]);
                        }
                        respawnsTable.Insert(respawnsTable.Rows.Count, allRows[i]);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(MyGuiScreenMedicals))]
        public class AddRespawnPointsPatch
        {
            public static MethodBase TargetMethod()
            {
                return AccessTools.FirstMethod(typeof(MyGuiScreenMedicals),
                    m => m.Name.Contains("<RefreshMedicalRooms>g__AddMedicalRespawnPoints"));
            }

            public static void Postfix(MyGuiControlTable ___m_respawnsTable)
            {
                respawnsTable = ___m_respawnsTable;
                if (___m_respawnsTable.RowsCount > 0)
                {
                    allRows = ___m_respawnsTable.Rows.OrderBy(i => i.GetCell(0).Text.ToString()).ToList();
                    if (searchBox != null && !String.IsNullOrWhiteSpace(searchBox.TextBox.Text))
                    {
                        for (int i = 0; i < ___m_respawnsTable.RowsCount;)
                        {
                            if (!___m_respawnsTable.Rows[i].GetCell(0).Text.ToString().Contains(searchBox.TextBox.Text, StringComparison.OrdinalIgnoreCase))
                            {
                                ___m_respawnsTable.Remove(___m_respawnsTable.Rows[i]);
                            }
                            else i++;
                        }
                    }

                    List<MyGuiControlTable.Row> ordered = ___m_respawnsTable.Rows.OrderBy(i => i.GetCell(0).Text.ToString()).ToList();
                    for (int i = 0; i < ordered.Count; i++)
                    {
                        ___m_respawnsTable.Remove(ordered[i]);
                        
                        ___m_respawnsTable.Insert(i, ordered[i]);
                    }
                }
                else
                {
                    allRows = null;
                }
            }
        }

        [HarmonyPatch(typeof(MyGuiScreenMedicals), "OnClosed")]
        public class OnClosedPatch
        {
            public static void Postfix()
            {
                searchBox.OnTextChanged -= OnSearchBoxTextChanged;
                searchBox = null;
                respawnsTable = null;
                allRows = null;
            }
        }
    }
}
