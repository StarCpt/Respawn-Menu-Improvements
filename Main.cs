using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VRage.Plugins;
using HarmonyLib;

namespace RespawnMenuImprovements
{
    public class Main : IPlugin
    {
        public void Init(object gameInstance)
        {
            new Harmony("RespawnMenuImprovements").PatchAll(Assembly.GetExecutingAssembly());
        }

        public void Update()
        {

        }

        public void Dispose()
        {

        }
    }
}
