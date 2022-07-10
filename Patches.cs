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
        private static StringBuilder respawnPointTooltip { get; } = new StringBuilder();
        private static long m_restrictedRespawn = -1;
        private static SortType sortStatus = SortType.None;
        private static DateTime lastTableSortedTime = DateTime.MinValue;
        private static MyGuiControlCombobox playersFilterDropdown = null;

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
            if (allRows == null || respawnsTable == null)
            {
                return;
            }

            //allRows = SortList(allRows, sortStatus);
            SortListInPlace(allRows, sortStatus);

            ApplySearchFilter(respawnsTable, newText);
            ApplyOwnerFilter(respawnsTable, playersFilterDropdown.GetSelectedKey());
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

        private static void SortRespawnsTable(MyGuiControlTable table, SortType type)
        {
            if (table == null || type == SortType.None)
            {
                return;
            }

            MyGuiControlTable.Row selectedRow = table.SelectedRow;
            sortStatus = type;
            lastTableSortedTime = DateTime.UtcNow;
            List<MyGuiControlTable.Row> ordered = SortList(table.Rows.Where(i => i.UserData is MySpaceRespawnComponent.MyRespawnPointInfo), type);
            SortInternal();

            void SortInternal()
            {
                for (int i = 0; i < ordered.Count; i++)
                {
                    table.Remove(ordered[i]);
                    table.Insert(table.RowsCount, ordered[i]);
                }
                if (selectedRow != null && table.Rows.Contains(selectedRow))
                {
                    table.SelectedRow = selectedRow;
                }
            }
        }

        private static List<MyGuiControlTable.Row> SortList(IEnumerable<MyGuiControlTable.Row> list, SortType type)
        {
            if (type == SortType.NameAscending)
            {
                return list.OrderBy(i => i.GetCell(0).Text.ToString()).ToList();
            }
            else if (type == SortType.NameDescending)
            {
                return list.OrderByDescending(i => i.GetCell(0).Text.ToString()).ToList();
            }
            else if (type == SortType.OwnerAscending)
            {
                return list.OrderBy(i => i.GetCell(1).Text.ToString()).ThenBy(i => i.GetCell(0).Text.ToString()).ToList();
            }
            else if (type == SortType.OwnerDescending)
            {
                return list.OrderByDescending(i => i.GetCell(1).Text.ToString()).ThenByDescending(i => i.GetCell(0).Text.ToString()).ToList();
            }
            else
            {
                return list.ToList();
            }
        }

        private static void SortListInPlace(List<MyGuiControlTable.Row> list, SortType type)
        {
            if (type == SortType.NameAscending)
            {
                list.Sort((x, y) => x.GetCell(0).Text.ToString().CompareTo(y.GetCell(0).Text.ToString()));
            }
            else if (type == SortType.NameDescending)
            {
                list.Sort((y, x) => x.GetCell(0).Text.ToString().CompareTo(y.GetCell(0).Text.ToString()));
            }
            else if (type == SortType.OwnerAscending)
            {
                list.Sort((x, y) =>
                {
                    int result = x.GetCell(1).Text.ToString().CompareTo(y.GetCell(1).Text.ToString());
                    if (result != 0) return result;
                    else return x.GetCell(0).Text.ToString().CompareTo(y.GetCell(0).Text.ToString());
                });
            }
            else if (type == SortType.OwnerDescending)
            {
                list.Sort((y, x) =>
                {
                    int result = x.GetCell(1).Text.ToString().CompareTo(y.GetCell(1).Text.ToString());
                    if (result != 0) return result;
                    else return x.GetCell(0).Text.ToString().CompareTo(y.GetCell(0).Text.ToString());
                });
            }
        }

        private static void PlayersFilterDropdown_ItemSelected()
        {
            long selectedKey = playersFilterDropdown.GetSelectedKey();

            if (searchBox != null) ApplySearchFilter(respawnsTable, searchBox.SearchText);
            ApplyOwnerFilter(respawnsTable, selectedKey);
        }

        private static void ApplySearchFilter(MyGuiControlTable table, string searchTerm)
        {
            MyGuiControlTable.Row selectedRow = respawnsTable.SelectedRow;
            if (String.IsNullOrWhiteSpace(searchTerm))
            {
                for (int i = 0; i < allRows.Count; i++)
                {
                    if (table.Rows.Contains(allRows[i]))
                    {
                        table.Remove(allRows[i]);
                    }
                    table.Insert(table.Rows.Count, allRows[i]);
                }

                if (selectedRow != null && table.Rows.Contains(selectedRow))
                {
                    table.SelectedRow = selectedRow;
                }
            }
            else
            {
                for (int i = 0; i < allRows.Count; i++)
                {
                    if (table.Rows.Contains(allRows[i]))
                    {
                        table.Remove(allRows[i]);
                    }
                    if (allRows[i].GetCell(0).Text.ToString().Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    {
                        table.Insert(table.Rows.Count, allRows[i]);
                    }
                }
            }

            if (selectedRow != null && table.Rows.Contains(selectedRow))
            {
                table.SelectedRow = selectedRow;
            }
        }

        private static void ApplyOwnerFilter(MyGuiControlTable table, long ownerId)
        {
            if (ownerId == 0)
            {
                return;
            }

            for (int i = 0; i < table.RowsCount;)
            {
                if (table.Rows[i].UserData is MySpaceRespawnComponent.MyRespawnPointInfo userData && userData.OwnerId != ownerId)
                {
                    table.Remove(table.Rows[i]);
                }
                else i++;
            }
        }

        [HarmonyPatch(typeof(MyGuiScreenMedicals), "RecreateControlsRespawn")]
        public class RecreateControlsRespawnPatch
        {
            public static void Postfix(MyGuiScreenMedicals __instance)
            {
                //searchBox = new MyGuiControlSearchBox(new Vector2(0f, 0.244f), new Vector2(0.3593f, 0f));
                searchBox = new MyGuiControlSearchBox(new Vector2(-0.07465f, 0.244f), new Vector2(0.21f, 0f));
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

            public static void Postfix(MyGuiScreenMedicals __instance, MyGuiControlTable ___m_respawnsTable, long ___m_restrictedRespawn)
            {
                respawnsTable = ___m_respawnsTable;
                m_restrictedRespawn = ___m_restrictedRespawn;
                ___m_respawnsTable.ItemMouseOver += OnRespawnTableItemMouseOver;
                ___m_respawnsTable.ItemFocus += OnRespawnTableItemMouseOver;
                ___m_respawnsTable.ColumnClicked += OnRespawnTableColumnClicked;
                if (___m_respawnsTable.RowsCount > 0)
                {
                    allRows = SortList(___m_respawnsTable.Rows.Where(i => i.UserData is MySpaceRespawnComponent.MyRespawnPointInfo), sortStatus != SortType.None ? sortStatus : SortType.NameAscending);

                    long selectedKey = 0;
                    if (playersFilterDropdown != null)
                    {
                        __instance.RemoveControl(playersFilterDropdown);
                        selectedKey = playersFilterDropdown.GetSelectedKey();
                        playersFilterDropdown.ClearItems();
                    }
                    else
                    {
                        playersFilterDropdown = new MyGuiControlCombobox(new Vector2(0.10760f, 0.248f), new Vector2(0.146f, 0.1f));
                        playersFilterDropdown.ItemSelected += PlayersFilterDropdown_ItemSelected;
                    }

                    playersFilterDropdown.AddItem(0L, "No Filter", sortOrder: 0, sort: false);

                    for (int i = 0; i < allRows.Count; i++)
                    {
                        var data = (MySpaceRespawnComponent.MyRespawnPointInfo)allRows[i].UserData;
                        var ownerFaction = MySession.Static.Factions.TryGetPlayerFaction(data.OwnerId);
                        var ownerDisplayName = GetOwnerDisplayName(data.OwnerId);

                        if (playersFilterDropdown.TryGetItemByKey(data.OwnerId) == null)
                        {
                            playersFilterDropdown.AddItem(data.OwnerId, ownerDisplayName, toolTip: ownerFaction != null ? ownerFaction.Tag + " " + ownerFaction.Name : "No Faction");
                        }

                        allRows[i].GetCell(1).Text.Clear().Append(ownerDisplayName);
                    }

                    playersFilterDropdown.CustomSortItems((x, y) =>
                    {
                        if (x.Key == 0) return -1;
                        else if (y.Key == 0) return 1;
                        else return x.Value.ToString().CompareTo(y.Value.ToString());
                    });

                    if (playersFilterDropdown.TryGetItemByKey(selectedKey) != null)
                    {
                        playersFilterDropdown.SelectItemByKey(selectedKey);
                    }

                    __instance.AddControl(playersFilterDropdown);

                    if (searchBox != null) ApplySearchFilter(___m_respawnsTable, searchBox.SearchText);
                    ApplyOwnerFilter(___m_respawnsTable, selectedKey);

                    ___m_respawnsTable.SetColumnName(1, new StringBuilder("Owner"));//original: "Available in"
                    SortRespawnsTable(___m_respawnsTable, sortStatus != SortType.None ? sortStatus : SortType.NameAscending);
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
                    playersFilterDropdown.ItemSelected -= PlayersFilterDropdown_ItemSelected;
                }
                catch { }

                searchBox = null;
                respawnsTable = null;
                allRows = null;
                m_restrictedRespawn = -1;
                sortStatus = SortType.None;
                playersFilterDropdown = null;
            }
        }
    }
}
