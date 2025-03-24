using Dalamud.Hooking;
using System.Numerics;
using System.Runtime.InteropServices;

namespace PantiesTech.PluginModule
{
    internal class DoActionHack : IPluginModule
    {
        public static bool Enabled { get; }

        private static IntPtr ActionManager;

        public class DoActionEventArgs
        {
            public IntPtr actionManager;
            public int actionType;      // 动作类型（1=技能，2=物品等）
            public uint actionId;       // 具体动作/物品ID
            public long TargetId;       // 目标ID（0xE0000000表示无目标）
            public int arg5;            // 未命名参数（通常为0）
            public uint comeflag;       // 来源标志（0=键盘，1=队列，2=宏）
            public int arg7;            // 未知参数
            public IntPtr arg8;         // 指针参数
            public Operate operate;     // 操作控制枚举
            public byte ret;            // 原始返回值
        }
        public enum Operate
        {
            Default,        // 正常执行
            Skip,           // 跳过预处理
            SkipToEnd,      // 直接跳转到结尾
            Return          // 立即返回
        }
        public delegate void DoActionEventHandler(DoActionEventArgs e);

        public static event DoActionEventHandler PreDoAction;
        public static event DoActionEventHandler PostDoAction;

        #region DoAction

        // 钩子委托定义
        private delegate byte DoActionDelegate(IntPtr actionManager, int actionType, uint actionId, long TargetId, int arg5, uint comeflag, int arg7, IntPtr arg8);
        private static Hook<DoActionDelegate> DoActionHook;
        private static Vector3 actionArea = new Vector3();
        /// <summary>
        /// DoAction Hook 后处理函数 钩子回调函数
        /// </summary>
        /// <param name="actionManager">ActionManager</param>
        /// <param name="actionType">技能类型</param>
        /// <param name="actionId">技能Id</param>
        /// <param name="TargetId">目标Id</param>
        /// <param name="arg5">unknown</param>
        /// <param name="comeFlag">来源标志，0=游戏输入，1=队列，2=宏</param>
        /// <param name="arg7"></param>
        /// <param name="arg8"></param>
        /// <returns></returns>
        private static unsafe byte DoActionDetour(IntPtr actionManager, int actionType, uint actionId, long TargetId, int arg5, uint comeFlag, int arg7, IntPtr arg8)
        {
            
            if (actionType == 5)
            {
                //无限疾跑
                if (actionId == 4)
                {
                    // 拦截疾跑动作
                    DoActionLocation(5, 4, new(0, 0, 0));
                    // 阻断原始调用
                    return 0;
                }
                //即刻返回
                //if (actionId == 8)
                //{
                //    DoActionLocation(5, 8, new(0, 0, 0));
                //    return 0;
                //}
                //FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance()->UseActionLocation(ActionType.GeneralAction, 4);
                //var rst2 = DoActionHook.Original(actionManager, actionType, actionId, TargetId, arg5, comeFlag, arg7, arg8);
                
            }


            return DoActionHook.Original(actionManager, actionType, actionId, TargetId, arg5, comeFlag, arg7, arg8);
            

        }


        /// <summary>
        /// 执行技能，会触发本插件Hook
        /// </summary>
        /// <param name="actionType"></param>
        /// <param name="ActionId"></param>
        /// <param name="targetId"></param>
        public static void DoAction(int actionType, uint ActionId, long targetId)
        {
            if (targetId == 0)
            {
                targetId = 0xE0000000;
            }
            Vector3 posion = Vector3.Zero;
            unsafe
            {
                DoActiondLocationDector(ActionManager, actionType, ActionId, targetId, &posion, 0);
            }
        }

        /// <summary>
        /// 绕过本插件Hook执行技能
        /// </summary>
        /// <param name="actionType"></param>
        /// <param name="ActionId"></param>
        /// <param name="targetId"></param>
        public static void DoActionOriginal(int actionType, uint ActionId, long targetId)
        {
            DoActionHook.Original(ActionManager, actionType, ActionId, targetId, 0, 0, 0, IntPtr.Zero);
        }
        /// <summary>
        /// 使用物品，会触发本插件Hook
        /// </summary>
        /// <param name="ItemId"></param>
        /// <param name="targetId"></param>
        /// <param name="Hq"></param>
        public static void UseItem(uint ItemId, long targetId, bool Hq = false)
        {
            if (targetId == 0)
            {
                targetId = 0xE0000000;
            }
            if (ItemId < 1000000 & Hq)
            {
                ItemId = ItemId + 1000000;
            }
            DoActionDetour(ActionManager, 2, ItemId, targetId, 65535, 0, 0, IntPtr.Zero);
        }
        #endregion

        #region DoActionLocation

        private static Hook<DoActiondLocationDelegate> DoActionLocationHook;
        private unsafe delegate int DoActiondLocationDelegate(IntPtr actionManager, int actionType, uint actionId, long TargetId, Vector3* postionPtr, uint arg6);
        private static unsafe int DoActiondLocationDector(IntPtr actionManager, int actionType, uint actionId, long TargetId, Vector3* location, uint extraParam)
        {
            
            var ret = DoActionLocationHook.Original(actionManager, actionType, actionId, TargetId, location, extraParam);
            //PluginLog.Debug($"ret:{ret}|actionManager:{actionManager:X8}|actionType:{actionType}|actionId:{actionId}|TargetId:{TargetId:X8}|postion:{*postionPtr}|arg6:{arg6}");
            
            return ret;
        }

        public static void DoActionLocation(int actionType, uint ActionId, Vector3 posion)
        {
            unsafe
            {
                DoActiondLocationDector(ActionManager, actionType, ActionId, 0xE0000000, &posion, 0);
            }
        }
        public static void DoActionLocation(int actionType, uint ActionId, uint targetId, Vector3 posion, uint extraParam)
        {
            unsafe
            {
                DoActiondLocationDector(ActionManager, actionType, ActionId, targetId, &posion, extraParam);
            }
        }
        #endregion
        public static void Init()
        {

            try
            {
                //FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Addresses.UseActionLocation
                var DoActionLocationPtr = Service.Scanner.ScanText("E8 ?? ?? ?? ?? 41 3A C5 0F 85 ?? ?? ?? ?? ?? ??");
                unsafe
                {
                    DoActionLocationHook = Service.GameHook.HookFromAddress<DoActiondLocationDelegate>(DoActionLocationPtr, DoActiondLocationDector);
                }
                DoActionLocationHook.Enable();

                //FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Addresses.UseAction
                var DoActionPtr = Service.Scanner.ScanText("E8 ?? ?? ?? ?? B0 01 EB B6 ?? ?? ?? ?? ?? ?? ??");
                DoActionHook = Service.GameHook.HookFromAddress<DoActionDelegate>(DoActionPtr, DoActionDetour);
                DoActionHook.Enable();


                unsafe
                {
                    // 内存操作实现
                    ActionManager = new(FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance());
                }

            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex.Message);
                Service.PluginLog.Error(ex.StackTrace);
            }



        }
        public static void Dispose()
        {
            DoActionHook.Disable();
            DoActionLocationHook.Disable();
            DoActionHook.Dispose();
            DoActionLocationHook.Dispose();
        }

        public static void DrawSetting()
        {
        }

        
    }
}
