using Il2CppGPNFramework;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using System;
using UnityEngine.UI;
using System.Collections.Generic;
using Il2CppDarwin.Outgame.Banner;
using Il2CppTMPro;
using HarmonyLib;
using UnityEngine.Networking;

using System.Reflection;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using Il2CppCysharp.Threading.Tasks;
using System.Threading;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System.Linq;
using Il2CppMono.Security.Cryptography;
using Il2CppSystem;

[assembly: MelonInfo(typeof(HackMod.MainMod), "HackMod", "1.0.3", "PlayerD")]
[assembly: MelonGame("TOKYO BEAST FZCO", "TOKYO BEAST")]

namespace HackMod
{
    public class MainMod : MelonMod
    {
        private EventSystem _eventSystem;
        private bool auto_script = false;
        private System.DateTime _lastActionTime = System.DateTime.MinValue;
        private System.DateTime run_time = System.DateTime.MinValue;
        private System.DateTime now_time = System.DateTime.MinValue;

        // 状态标志位
        private bool _isInQTE = false;

        // 按钮缓存系统
        private class ButtonCache
        {
            public string Path;
            public GameObject Object;
            public int Cooldown;
            public int WaitTime;
            public System.DateTime? LastEXPClickTime = null;
            public bool IsActive => Object != null && Object.activeInHierarchy;
        }

        private List<ButtonCache> _buttonCaches = new List<ButtonCache>();
        private int _currentCacheIndex = 0;
        private const int CACHE_COOLDOWN = 30;

        private GameObject _loadingPanel;
        private GameObject _popupWindow;
        private GameObject _note;
        private GameObject _outerGlow;
        private GameObject _qteButton;
        private GameObject _battle_camera_obj;
        private Image _loadingImage;
        private ButtonBase _buttonBase;
        private BannerButton _bannerButton;

        private int win, lost;
        private string stamina = "? / 10";
        private bool recorded = false;

        // 配置系统
        private Dictionary<string, int> _pathWaitTimes = new Dictionary<string, int>()
        {
            { "title_canvas/title_window/title_screen/start_button", 10000 },

            { "main_canvas/home_window/home_screen/home_top_panel/secondary_contents/user_exp_button_panel", 1000 },
            { "main_canvas/tournament_qualifier_window/tournament_qualifier_deck_screen/deck_footer_panel/right/tournament_battle_start_button", 5000 },
            { "main_canvas/contest_window/contest_deck_screen/root/deck_footer_panel/right/contest_battle_start_button", 5000 },
            { "main_canvas/dungeon_window/dungeon_deck_screen/root/deck_footer_panel/right/dungeon_battle_start_button", 5000 },

            { "main_canvas/battle_window/battle_screen/battle_pvp_start_panel/next_button", 1000 },
            { "main_canvas/battle_window/battle_screen/battle_result_panel/push_to_next_button_1", 1000 },
            { "main_canvas/battle_window/battle_screen/battle_result_panel/select_transition_widget/battle_button", 1000 },

            { "main_canvas/edge_window/edge_screen/edge_side_menu_panel/home_side_menu_button_battle", 3000 },

            { "battle_world(Clone)/battle_banner/banner3d_tournament", 3000}, // ARENA
            //{ "battle_world(Clone)/battle_banner/banner3d_contest", 3000}, //MATCHUP
            { "main_canvas/contest_window/contest_top_screen/tap_root/rental_battle_button", 1000 },

        };
        
        public override void OnInitializeMelon()
        {
            foreach (var kvp in _pathWaitTimes)
            {
                var cache = new ButtonCache
                {
                    Path = kvp.Key,
                    WaitTime = kvp.Value,
                    Cooldown = 0
                };
                _buttonCaches.Add(cache);
            }

            MelonLogger.Msg("模组初始化完成，按F2开启/关闭自动模式");
        }
        public override void OnSceneWasLoaded(int buildIndex, string _sceneName)
        {
            ResetAllCaches();
        }

        private void ResetAllCaches()
        {
            _loadingPanel = null;
            _note = null;
            _outerGlow = null;
            _qteButton = null;
            _loadingImage = null;
            _buttonBase = null;

            foreach (var cache in _buttonCaches)
            {
                cache.Object = null;
                cache.Cooldown = 0;
            }
            _currentCacheIndex = 0;
        }

        private int _waitTime = 1000;

        public override void OnUpdate()
        {
            try
            {
                if (Application.isFocused)
                {
                    Application.targetFrameRate = 60;
                }
                else
                {
                    Application.targetFrameRate = 15;
                }

                if (Input.GetKeyDown(KeyCode.F2))
                {
                    auto_script = !auto_script;
                    if (auto_script)
                    {
                        run_time = System.DateTime.Now;
                        win = 0; lost = 0;
                    }
                    Time.timeScale = 1;
                    MelonLogger.Msg($"自动模式已{(auto_script ? "开启" : "关闭")}");
                }

                if ((System.DateTime.Now - _lastActionTime).TotalMilliseconds < _waitTime) return;

                _waitTime = 1000;
                bool actionExecuted = false;
                if (_loadingPanel == null)
                {
                    _loadingPanel = GameObject.Find("darwin_singleton_manager/darwin_front_ui_manager/loading/loading_panel/loading_animation");
                    if (_loadingPanel != null)
                    {
                        _loadingImage = _loadingPanel.GetComponent<Image>();
                    }
                }

                if (_loadingPanel != null && _loadingImage != null && _loadingImage.enabled) return;

                UpdateQTEState();
                if (_isInQTE)
                {
                    if (auto_script) Time.timeScale = 0.3f;
                    if (!actionExecuted) actionExecuted = ProcessQTE();
                }
                else
                {
                    if (auto_script) Time.timeScale = 1f;
                    if (auto_script)
                    {
                        if (_popupWindow == null)
                        {
                            _popupWindow = GameObject.Find("main_canvas/popup_window/bg_button/Image");
                            if (_popupWindow == null)
                            {
                                _popupWindow = GameObject.Find("title_canvas/popup_window/bg_button/Image");
                            }
                        }
                        if (_popupWindow != null && _popupWindow.activeInHierarchy)
                        {
                            if (!actionExecuted) actionExecuted = TryClickButton(GameObject.Find("title_canvas/popup_window/server_error_popup/button_region/surface_s_button_1"));
                            if (!actionExecuted) actionExecuted = TryClickButton(GameObject.Find("main_canvas/popup_window/server_error_popup/button_region/surface_s_button_1"));
                            if (!actionExecuted) actionExecuted = TryClickButton(GameObject.Find("main_canvas/popup_window/deck_recommend_popup/button_region/surface_s_button"));
                            if (!actionExecuted) actionExecuted = TryClickButton(GameObject.Find("title_canvas/popup_window/error_network_failed_popup/button_region/outline_s_button"));
                            if (!actionExecuted) actionExecuted = TryClickButton(GameObject.Find("main_canvas/popup_window/error_network_failed_popup/button_region/outline_s_button"));

                            if (!actionExecuted) actionExecuted = TryClickButton(GameObject.Find("main_canvas/popup_window/user_exp_get_popup/button_region/surface_s_button"));
                            if (!actionExecuted) actionExecuted = TryClickButton(GameObject.Find("main_canvas/popup_window/user_exp_unavailable_popup/button_region/surface_s_button"));
                            if (!actionExecuted) actionExecuted = TryClickButton(GameObject.Find("title_canvas/popup_window/confirm_popup/button_region/surface_s_button"));
                            if (!actionExecuted) actionExecuted = TryClickButton(GameObject.Find("main_canvas/popup_window/confirm_popup/button_region/surface_s_button"));
                            if (!actionExecuted) actionExecuted = TryClickButton(GameObject.Find("main_canvas/popup_window/dungeon_jewel_stamina_start_popup/button_region/outline_s_button"));
                        }
                        else
                        {
                            //if (_battle_camera_obj == null)
                            //{
                            //    _battle_camera_obj = GameObject.Find("battle_system/cinemachine_broadcaster/battle_camera");
                            //}
                            //if (_battle_camera_obj != null)
                            //{
                            //    Camera _battle_camera = _battle_camera_obj.GetComponent<Camera>();
                            //    _battle_camera.cullingMask = 0;
                            //}
                            if (auto_script && !actionExecuted && _buttonCaches.Count > 0)
                            {
                                actionExecuted = ProcessNextButton();
                            }
                        }

                    }
                }

                if (actionExecuted) _lastActionTime = System.DateTime.Now;
            }
            catch { }
        }

        private void UpdateQTEState()
        {
            if (!_isInQTE && (_note == null || !_note.activeSelf))
            {
                _note = GameObject.Find("main_canvas/battle_window/qte_screen(Clone)/qte_panel/qte_action_panel/qte_action_fade_panel/note_area/note");
            }

            _isInQTE = _note != null && _note.activeSelf;
        }

        private bool ProcessQTE()
        {
            if (_outerGlow == null)
            {
                _outerGlow = GameObject.Find("main_canvas/battle_window/qte_screen(Clone)/qte_panel/qte_action_panel/qte_action_fade_panel/note_area/note/ui_polygon_glow/glow/outer_glow");
            }

            if (_outerGlow != null && _outerGlow.activeSelf)
            {
                if (_qteButton == null)
                {
                    _qteButton = GameObject.Find("main_canvas/battle_window/qte_screen(Clone)/qte_panel/qte_action_panel/qte_action_fade_panel/note_area/note/button");
                }

                if (TryClickButton(_qteButton))
                {
                    //MelonLogger.Msg($"Camera {Camera.main.transform.rotation.x}, {Camera.main.transform.rotation.y}");
                    MelonLogger.Msg($"已点击QTE按钮");
                    return true;
                }
            }
            return false;
        }

        private bool ProcessNextButton()
        {
            if (_buttonCaches.Count == 0) return false;

            var cache = _buttonCaches[_currentCacheIndex];
            _currentCacheIndex = (_currentCacheIndex + 1) % _buttonCaches.Count;

            if (cache.Path == "main_canvas/home_window/home_screen/home_top_panel/secondary_contents/user_exp_button_panel" &&
                cache.LastEXPClickTime.HasValue &&
                (System.DateTime.Now - cache.LastEXPClickTime.Value).TotalHours < 1)
            {
                return false;
            }

            if (cache.Cooldown > 0)
            {
                cache.Cooldown--;
                return false;
            }

            if (cache.Object == null)
            {
                cache.Object = GameObject.Find(cache.Path);
                cache.Cooldown = cache.Object == null ? CACHE_COOLDOWN : 0;
            }

            if (cache.Object != null && cache.Object.activeInHierarchy)
            {
                if (cache.Path == "main_canvas/contest_window/contest_deck_screen/root/deck_footer_panel/right/contest_battle_start_button"
                    )
                {
                    //MelonLogger.Msg($"{cache.Path}: {cache.Object.transform.position.x}");
                    if (cache.Object.transform.position.x != 1120.1111f)
                    {
                        return false;
                    }
                    SimpleButton _simple = cache.Object.GetComponent<SimpleButton>();
                    if (_simple != null && _simple.State == SimpleButtonElement.SimpleButtonState.Disabled)
                    {
                        ExtendedText _power_value = GameObject.Find("main_canvas/contest_window/contest_deck_screen/root/deck_footer_panel/deck_footer_panel_center/deck_power[LayoutGroup=Horizontal,Spacing=6]/power_value").GetComponent<ExtendedText>();
                        GameObject _recommend_button = GameObject.Find("main_canvas/contest_window/contest_deck_screen/root/deck_footer_panel/left/recommend_button");
                        if (_recommend_button.activeInHierarchy)
                        {
                            if (_power_value.m_text != "0")
                            {
                                TryClickButton(GameObject.Find("main_canvas/contest_window/contest_deck_screen/root/deck_footer_panel/left/all_remove_button"));
                                return true;
                            }
                            else if (_power_value.m_text == "0")
                            {
                                TryClickButton(_recommend_button);
                                return true;
                            }
                        }

                        return false;
                    }
                }

                if (cache.Path == "main_canvas/battle_window/battle_screen/battle_result_panel/push_to_next_button_1")
                {
                    GameObject _win_Trainer = GameObject.Find("main_canvas/battle_window/battle_screen/battle_result_panel/TrainerScoreWidget/main_region/result_texts/win_text");
                    GameObject _win_Arena = GameObject.Find("main_canvas/battle_window/battle_screen/battle_result_panel/ArenaScoreWidget/main_region/result_texts/win_text");
                    //MelonLogger.Msg($"_win_Trainer: {_win_Trainer.activeInHierarchy}, {_win_Trainer.transform.position.x}");
                    //MelonLogger.Msg($"_win_Arena: {_win_Arena.activeInHierarchy}, {_win_Arena.transform.position.x}");
                    if (!recorded)
                    {
                        if (_win_Trainer.activeInHierarchy)
                        {
                            if (_win_Trainer.transform.position.x <= 322)
                            {
                                win += 1;
                                recorded = true;
                            }
                            else
                            {
                                lost += 1;
                                recorded = true;
                            }
                        }
                        else if (_win_Arena.activeInHierarchy)
                        {
                            if (_win_Arena.transform.position.x <= 322)
                            {
                                win += 1;
                                recorded = true;
                            }
                            else
                            {
                                lost += 1;
                                recorded = true;
                            }
                        }
                        
                    }
                }
                else if (cache.Path == "main_canvas/battle_window/battle_screen/battle_result_panel/select_transition_widget/battle_button")
                {
                    recorded = false;
                }
                else if (cache.Path == "main_canvas/edge_window/edge_screen/edge_side_menu_panel/home_side_menu_button_battle")
                {
                    GameObject _button_text = GameObject.Find("main_canvas/edge_window/edge_screen/edge_side_menu_panel/home_side_menu_button_battle/RectMask/text");
                    if (_button_text.activeInHierarchy)
                    {
                        TMP_Text _color = _button_text.GetComponent<TMP_Text>();
                        //MelonLogger.Msg($"_color: {_color.color.r}, {_color.color.g}, {_color.color.b}");
                        if (!(_color.color.r > 0.9 && _color.color.g > 0.9 && _color.color.b > 0.9))
                        {
                            return false;
                        }
                    }
                }
                else if (cache.Path == "battle_world(Clone)/battle_banner/banner3d_tournament" ||
                    cache.Path == "battle_world(Clone)/battle_banner/banner3d_contest")
                {
                    TryClickBanner(cache.Object);
                    return true;
                }
                else if(cache.Path == "main_canvas/popup_window/dungeon_jewel_stamina_start_popup/button_region/outline_s_button")
                {
                    TryClickButton(cache.Object);
                    TryClickButton(GameObject.Find("main_canvas/edge_window/edge_screen/edge_side_menu_panel/home_side_menu_button_home"));
                    //auto_script = false;
                    return true;
                }
                else if (cache.Path == "main_canvas/tournament_qualifier_window/tournament_qualifier_deck_screen/deck_footer_panel/right/tournament_battle_start_button")
                {
                    ExtendedText _stamina = GameObject.Find("main_canvas/edge_window/edge_screen/edge_header_panel/header_value_panel_pvp_stamina/header_value_panel/value").GetComponent<ExtendedText>();
                    stamina = _stamina.m_text;
                    SimpleButton _simple = cache.Object.GetComponent<SimpleButton>();
                    if (_simple != null && _simple.State == SimpleButtonElement.SimpleButtonState.Disabled)
                    {
                        if (_stamina.m_text != "0 / 10")
                        {
                            _simple.State = SimpleButtonElement.SimpleButtonState.Default;
                        }
                        //TryClickButton(GameObject.Find("main_canvas/edge_window/edge_screen/edge_side_menu_panel/home_side_menu_button_home"));
                        //auto_script = false;
                        return false;
                    }
                }

                if (TryClickButton(cache.Object))
                {
                    _waitTime = cache.WaitTime;
                    if (cache.Path == "main_canvas/home_window/home_screen/home_top_panel/secondary_contents/user_exp_button_panel")
                    {
                        cache.LastEXPClickTime = System.DateTime.Now;
                        MelonLogger.Msg("已点击经验按钮，冷却时间1小时");
                    }
                    MelonLogger.Msg($"已点击按钮: {cache.Path}");
                    return true;
                }

                cache.Object = null;
                cache.Cooldown = CACHE_COOLDOWN;
            }

            return false;
        }

        private bool TryClickBanner(GameObject button)
        {
            if (button == null || !button.activeInHierarchy)
                return false;

            if (_bannerButton == null || _bannerButton.gameObject != button)
            {
                _bannerButton = button.GetComponent<BannerButton>();
            }

            if (_bannerButton == null) return false;

            _bannerButton.OnTap();
            return true;
        }

        private bool TryClickButton(GameObject button)
        {
            if (button == null || !button.activeInHierarchy)
                return false;

            if (_buttonBase == null || _buttonBase.gameObject != button)
            {
                _buttonBase = button.GetComponent<ButtonBase>();
            }

            if (_buttonBase == null) return false;

            SimpleButton _simple = button.GetComponent<SimpleButton>();
            if (_simple != null && _simple.State == SimpleButtonElement.SimpleButtonState.Disabled)
            {
                return false;
            }

            EnsureEventSystemExists();
            SimulateButtonClick(_buttonBase);
            return true;
        }

        private void EnsureEventSystemExists()
        {
            if (EventSystem.current == null && _eventSystem == null)
            {
                var eventSystemObj = new GameObject("EventSystem");
                _eventSystem = eventSystemObj.AddComponent<EventSystem>();
                eventSystemObj.AddComponent<StandaloneInputModule>();
            }
        }

        private void SimulateButtonClick(ButtonBase button)
        {
            if (button == null) return;

            EnsureEventSystemExists();

            var pointerData = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 1,
                position = button.transform.position
            };
            button.OnPointerClick(pointerData);
        }
        
        public override void OnGUI()
        {

            GUIStyle onStyle = new GUIStyle
            {
                fontSize = 20,
                normal = { textColor = Color.green }
            };

            GUIStyle offSytel = new GUIStyle
            {
                fontSize = 20,
                normal = { textColor = Color.red }
            };

            GUIStyle shadowStyle = new GUIStyle
            {
                fontSize = 20,
                normal = { textColor = Color.black }
            };

            if (auto_script) now_time = System.DateTime.Now;
            string infoText = $"BATTLE: {win + lost} " +
                              $"WIN: {win} " +
                              $"RATE: {(win + lost == 0 ? "0.00" : (win / (float)(win + lost) * 100).ToString("0.00"))}% " +
                              $"TOTAL: {(now_time - run_time).ToString(@"hh\:mm\:ss")} " +
                              $"AVG: {System.TimeSpan.FromSeconds((now_time - run_time).TotalSeconds / (win + lost == 0 ? 1 : win + lost)).ToString(@"hh\:mm\:ss")} " +
                              $"STAMINA: {stamina} ";

            GUI.Label(new Rect(11, 11, 400, 30), infoText, shadowStyle);
            GUI.Label(new Rect(10, 10, 400, 30), infoText, auto_script ? onStyle : offSytel);
        }

        // Coroutine to monitor UnityWebRequest completions
        private static IEnumerator RequestMonitor()
        {
            while (true)
            {
                yield return null; // Wait for next frame
            }
        }
    }

    
    [HarmonyPatch(typeof(Il2Cpp.FastAES), nameof(Il2Cpp.FastAES.Encrypt), new[] { typeof(ReadOnlySpan<byte>), typeof(Il2CppStructArray<byte>), typeof(Il2CppStructArray<byte>), typeof(Il2CppStructArray<byte>) })]
    public class FastAESEncryptPatch
    {
        [HarmonyPostfix]
        static void prefix(Il2Cpp.FastAES __instance, int __result, ReadOnlySpan<byte> __0, Il2CppStructArray<byte> __1, Il2CppStructArray<byte> __2, Il2CppStructArray<byte> __3)
        {
            try
            {
                MelonLogger.Msg("=== 拦截 Il2Cpp.FastAES::Encrypt ===");
                MelonLogger.Msg($"实例: {__instance?.ToString() ?? "null"}");
                MelonLogger.Msg($"参数 0 (plain): ({__0?.ToArray().Length}){Encoding.UTF8.GetString(__0?.ToArray())}");
                MelonLogger.Msg($"参数 1 (key): {System.BitConverter.ToString(__1)}");
                MelonLogger.Msg($"参数 2 (iv): {System.BitConverter.ToString(__2)}");
                MelonLogger.Msg($"参数 2 (output): {System.BitConverter.ToString(__2)}");
                MelonLogger.Msg($"返回值: {__result.ToString()}");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Il2Cpp.FastAES::Decrypt 后缀钩子错误: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(Il2Cpp.FastAES), nameof(Il2Cpp.FastAES.Decrypt), new[] { typeof(ReadOnlySpan<byte>), typeof(Il2CppStructArray<byte>), typeof(Il2CppStructArray<byte>) })]
    public class FastAESDecryptPatch
    {
        [HarmonyPostfix]
        static void prefix(Il2Cpp.FastAES __instance, Il2CppStructArray<byte> __result, ReadOnlySpan<byte> __0, Il2CppStructArray<byte> __1, Il2CppStructArray<byte> __2)
        {
            try
            {
                MelonLogger.Msg("=== 拦截 Il2Cpp.FastAES::Decrypt ===");
                MelonLogger.Msg($"实例: {__instance?.ToString() ?? "null"}");
                MelonLogger.Msg($"参数 0 (cipher): ({__0?.ToArray().Length}){System.BitConverter.ToString(__0?.ToArray())}");
                MelonLogger.Msg($"参数 1 (key): {System.BitConverter.ToString(__1)}");
                MelonLogger.Msg($"参数 2 (iv): {System.BitConverter.ToString(__2)}");
                MelonLogger.Msg($"返回值: {Encoding.UTF8.GetString(__result)}");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Il2Cpp.FastAES::Decrypt 后缀钩子错误: {ex.Message}");
            }
        }
    }
    
    
    [HarmonyPatch(typeof(UnityWebRequest), nameof(UnityWebRequest.SendWebRequest))]
    public class UnityWebRequestPatch
    {
        [HarmonyPrefix]
        static void Prefix(UnityWebRequest __instance)
        {
            try
            {
                MelonLogger.Msg("=== 拦截 UnityWebRequest::SendWebRequest ===");
                MelonLogger.Msg($"请求: {__instance.method} {__instance.url}");
                MelonLogger.Msg($"查询参数: {(__instance.url.Contains("?") ? __instance.url.Substring(__instance.url.IndexOf("?") + 1) : "无")}");
                MelonLogger.Msg($"请求体: {(__instance.uploadHandler?.data != null ? Encoding.UTF8.GetString(__instance.uploadHandler.data) : "无")}");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"UnityWebRequest::SendWebRequest 前缀钩子错误: {ex.Message}");
            }
        }

        [HarmonyPostfix]
        static void Postfix(UnityWebRequest __instance, UnityWebRequestAsyncOperation __result)
        {
            try
            {
                //MelonLogger.Msg("=== 拦截 UnityWebRequest::SendWebRequest 响应 ===");
                //MelonLogger.Msg($"实例: {__instance?.ToString() ?? "null"}");
                //MelonLogger.Msg($"返回值: {__result?.ToString() ?? "null"}");

                if (__result != null && __instance != null)
                {
                    MelonCoroutines.Start(LogResponseWhenComplete(__result, __instance.url));
                }
                else
                {
                    MelonLogger.Error($"响应监控失败，URL: {__instance?.url ?? "未知"}");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"UnityWebRequest::SendWebRequest 后缀钩子错误: {ex.Message}");
            }
        }

        private static IEnumerator LogResponseWhenComplete(UnityWebRequestAsyncOperation asyncOp, string url)
        {
            yield return asyncOp;
            try
            {
                var request = asyncOp.webRequest;
                if (request == null)
                {
                    MelonLogger.Error($"响应监控失败，URL: {url}");
                    yield break;
                }

                MelonLogger.Msg($"=== {url} 响应 ===");
                MelonLogger.Msg($"状态码: {request.responseCode}");
                MelonLogger.Msg($"响应体: ({request.downloadHandler?.data.Length}){(request.downloadHandler?.data != null ? Encoding.UTF8.GetString(request.downloadHandler.data) : "无")}");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"响应监控错误，URL: {url}, 错误: {ex.Message}");
            }
        }
    }
    
}