using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Sandbox.Game.Localization;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using SpaceEngineers.Game.GUI;
using SpaceEngineers.Game.World;
using VRage;
using VRageMath;

namespace RespawnMenuImprovements
{
    public class Patches
    {
        private static MyGuiControlSearchBox searchBox = null;
        private static MyGuiControlTable respawnsTable = null;
        private static List<MyGuiControlTable.Row> allRows = null;
        private static StringBuilder respawnPointTooltip = new StringBuilder();
        private static long m_restrictedRespawn = -1;
        private static SortType sortStatus = SortType.None;
        private static DateTime lastTableSortedTime = DateTime.MinValue;

        private enum SortType
        {
            None = 0,
            NameAscending = 1,
            NameDescending = 2,
            OwnerAscending = 4,
            OwnerDescending = 8,
        }

        public static void OnSearchBoxTextChanged(string newText)
        {
            if (allRows != null && respawnsTable != null)
            {
                SortList(ref allRows, sortStatus);
                MyGuiControlTable.Row selectedRow = respawnsTable.SelectedRow;

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

                if (selectedRow != null && respawnsTable.Rows.Contains(selectedRow))
                {
                    respawnsTable.SelectedRow = selectedRow;
                }
            }
        }

        private static StringBuilder GetOwnerDisplayName(long owner)
        {
            if (owner == 0L)
                return MyTexts.Get(MySpaceTexts.BlockOwner_Nobody);
            MyIdentity identity = Sync.Players.TryGetIdentity(owner);
            return identity != null ? new StringBuilder(identity.DisplayName) : MyTexts.Get(MySpaceTexts.BlockOwner_Unknown);
        }

        private static void OnRespawnTableItemMouseOver(MyGuiControlTable.Row row)
        {
            respawnPointTooltip.Clear();
            if (row.UserData is MySpaceRespawnComponent.MyRespawnPointInfo medicalRoom)
            {
                bool isRestricted = m_restrictedRespawn == 0L || medicalRoom.MedicalRoomId == m_restrictedRespawn;
                respawnPointTooltip.AppendLine(medicalRoom.MedicalRoomName);
                respawnPointTooltip.Append("Status: " + (isRestricted ? MyTexts.Get(MySpaceTexts.ScreenMedicals_RespawnShipReady) : MyTexts.Get(MySpaceTexts.ScreenMedicals_RespawnShipNotReady)));
                respawnsTable.SetToolTip(respawnPointTooltip.ToString());
            }
            else
            {
                respawnsTable.HideToolTip();
            }
        }

        private static void OnRespawnTableColumnClicked(MyGuiControlTable table, int columnIndex)
        {
            if ((DateTime.UtcNow - lastTableSortedTime).TotalMilliseconds > 10)
            {
                if (columnIndex == 0)
                {
                    if (sortStatus == SortType.NameAscending)
                    {
                        SortRespawnsTable(table, SortType.NameDescending);
                    }
                    else
                    {
                        SortRespawnsTable(table, SortType.NameAscending);
                    }
                }
                else if (columnIndex == 1)
                {
                    if (sortStatus == SortType.OwnerAscending)
                    {
                        SortRespawnsTable(table, SortType.OwnerDescending);
                    }
                    else
                    {
                        SortRespawnsTable(table, SortType.OwnerAscending);
                    }
                }
            }
        }

        private static void SortRespawnsTable(MyGuiControlTable table, SortType type, bool firstAdd = false)
        {
            if (table == null)
            {
                return;
            }

            MyGuiControlTable.Row selectedRow = table.SelectedRow;
            sortStatus = type;
            lastTableSortedTime = DateTime.UtcNow;
            List<MyGuiControlTable.Row> ordered;

            if (type == SortType.NameAscending)
            {
                ordered = table.Rows.Where(i => i.UserData is MySpaceRespawnComponent.MyRespawnPointInfo).OrderBy(i => i.GetCell(0).Text.ToString()).ToList();
                SortInternal();
            }
            else if (type == SortType.NameDescending)
            {
                ordered = table.Rows.Where(i => i.UserData is MySpaceRespawnComponent.MyRespawnPointInfo).OrderByDescending(i => i.GetCell(0).Text.ToString()).ToList();
                SortInternal();
            }
            else if (type == SortType.OwnerAscending)
            {
                ordered = table.Rows.Where(i => i.UserData is MySpaceRespawnComponent.MyRespawnPointInfo).OrderBy(i => i.GetCell(1).Text.ToString()).ThenBy(i => i.GetCell(0).Text.ToString()).ToList();
                SortInternal();
            }
            else if (type == SortType.OwnerDescending)
            {
                ordered = table.Rows.Where(i => i.UserData is MySpaceRespawnComponent.MyRespawnPointInfo).OrderByDescending(i => i.GetCell(1).Text.ToString()).ThenByDescending(i => i.GetCell(0).Text.ToString()).ToList();
                SortInternal();
            }

            void SortInternal()
            {
                for (int i = 0; i < ordered.Count; i++)
                {
                    table.Remove(ordered[i]);
                    if (firstAdd)
                    {
                        ordered[i].GetCell(1).Text.Clear().Append(GetOwnerDisplayName(((MySpaceRespawnComponent.MyRespawnPointInfo)ordered[i].UserData).OwnerId));
                    }
                    table.Insert(table.RowsCount, ordered[i]);
                }
                if (selectedRow != null && table.Rows.Contains(selectedRow))
                {
                    table.SelectedRow = selectedRow;
                }
            }
        }

        private static void SortList(ref List<MyGuiControlTable.Row> list, SortType type)
        {
            sortStatus = type;
            if (type == SortType.NameAscending)
            {
                list = list.OrderBy(i => i.GetCell(0).Text.ToString()).ToList();
            }
            else if (type == SortType.NameDescending)
            {
                list = list.OrderByDescending(i => i.GetCell(0).Text.ToString()).ToList();
            }
            else if (type == SortType.OwnerAscending)
            {
                list = list.OrderBy(i => i.GetCell(1).Text.ToString()).ThenBy(i => i.GetCell(0).Text.ToString()).ToList();
            }
            else if (type == SortType.OwnerDescending)
            {
                list = list.OrderByDescending(i => i.GetCell(1).Text.ToString()).ThenByDescending(i => i.GetCell(0).Text.ToString()).ToList();
            }
        }

        [HarmonyPatch(typeof(MyGuiScreenMedicals), "RecreateControlsRespawn")]
        public class RecreateControlsRespawnPatch
        {
            public static void Postfix(MyGuiScreenMedicals __instance)
            {
                searchBox = new MyGuiControlSearchBox(new Vector2(0f, 0.244f), new Vector2(0.3593f, 0f));
                searchBox.OnTextChanged += OnSearchBoxTextChanged;

                __instance.Controls.Add(searchBox);
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

            public static void Postfix(MyGuiControlTable ___m_respawnsTable, long ___m_restrictedRespawn)
            {
                respawnsTable = ___m_respawnsTable;
                m_restrictedRespawn = ___m_restrictedRespawn;
                ___m_respawnsTable.ItemMouseOver += OnRespawnTableItemMouseOver;
                ___m_respawnsTable.ItemFocus += OnRespawnTableItemMouseOver;
                ___m_respawnsTable.ColumnClicked += OnRespawnTableColumnClicked;
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

                    //___m_respawnsTable.SetColumnName(0, new StringBuilder("Name"));
                    ___m_respawnsTable.SetColumnName(1, new StringBuilder("Owner"));//original: "Available in"
                    SortRespawnsTable(___m_respawnsTable, SortType.NameAscending, true);
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
                try
                {
                    searchBox.OnTextChanged -= OnSearchBoxTextChanged;
                    respawnsTable.ItemMouseOver -= OnRespawnTableItemMouseOver;
                    respawnsTable.ItemFocus -= OnRespawnTableItemMouseOver;
                    respawnsTable.ColumnClicked -= OnRespawnTableColumnClicked;
                }
                catch { }

                searchBox = null;
                respawnsTable = null;
                allRows = null;
                m_restrictedRespawn = -1;
                sortStatus = SortType.None;
            }
        }
    }
}
