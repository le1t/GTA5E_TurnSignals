using System;
using System.Windows.Forms;
using GTA;
using GTA.Native;

public class GTA5E_TurnSignals : Script
{
    // ==================== Состояния поворотников ====================
    private bool leftOn = false;
    private bool rightOn = false;
    private bool hazardOn = false;

    // ==================== Переменные для синхронизации ====================
    private DateTime leftStartTime = DateTime.MinValue;
    private DateTime rightStartTime = DateTime.MinValue;

    // ==================== Переменные для автоотключения ====================
    private DateTime leftOnTime = DateTime.MinValue;
    private DateTime rightOnTime = DateTime.MinValue;
    private int leftStableFrames = 0;
    private int rightStableFrames = 0;
    private bool leftWasOutOfNeutral = false;
    private bool rightWasOutOfNeutral = false;

    // ==================== НАСТРОЙКИ ====================
    private const float NeutralThresholdDeg = 20.0f;
    private const int StableFramesRequired = 4;
    private const float MinOnTimeSeconds = 2.5f;
    private const float MinSpeedKmh = 5.0f;
    private const float SteeringSpeedThreshold = 15.0f;

    // ==================== НАСТРОЙКИ СИНХРОНИЗАЦИИ ФАР ====================
    private const float FullBlinkCycle = 0.90f;  // Полный цикл (свет + пауза)
    private const float LightOnDuration = 0.45f; // Время, пока фара горит
    private const float StepInterval = 0.15f;    // Задержка загорания одной стрелки

    // Символы стрелок
    private const string LeftArrowChar = "«"; 
    private const string RightArrowChar = "»";

    // ==================== Служебные переменные ====================
    private float lastSteeringDeg = 0f;
    private DateTime lastSteeringTime = DateTime.Now;
    private int lastVehicleHandle = 0;

    // ==================== КООРДИНАТЫ И РАЗМЕР ====================
    private readonly float indicatorX = 0.50f;          // Центр экрана
    private readonly float indicatorY = 0.90f;          // Внизу экрана
    private readonly float horizontalOffset = 0.035f;   // Отступ от центра
    private readonly float arrowSpacing = 0.012f;       // Шаг между стрелками
    private readonly float indicatorScale = 0.75f;      // Размер шрифта

    // ========================================================================
    public GTA5E_TurnSignals()
    {
        this.KeyDown += OnKeyDown;
        this.Tick += OnTick;
        this.Interval = 0;
    }

    // ---------------------------- Основной цикл ----------------------------
    private void OnTick(object sender, EventArgs e)
    {
        Ped player = Game.Player.Character;
        if (!player.IsInVehicle())
        {
            if (lastVehicleHandle != 0)
            {
                ResetStates();
                lastVehicleHandle = 0;
            }
            return;
        }

        Vehicle veh = player.CurrentVehicle;
        if (veh == null || !veh.Exists()) return;

        if (veh.Handle != lastVehicleHandle)
        {
            ResetStates();
            lastVehicleHandle = veh.Handle;
        }

        float speedKmh = veh.Speed * 3.6f;
        if (speedKmh < MinSpeedKmh || hazardOn)
        {
            leftStableFrames = 0;
            rightStableFrames = 0;
        }

        // ----- Автоотключение -----
        if (speedKmh >= MinSpeedKmh && !hazardOn)
        {
            float steeringDeg = veh.SteeringAngle;
            float steeringSpeed = CalculateSteeringSpeed(steeringDeg);

            // Левый
            if (leftOn)
            {
                bool inNeutral = Math.Abs(steeringDeg) < NeutralThresholdDeg;
                bool steeringMoving = Math.Abs(steeringSpeed) > SteeringSpeedThreshold;
                if (!inNeutral || steeringMoving)
                    leftWasOutOfNeutral = true;

                bool minTimePassed = (DateTime.Now - leftOnTime).TotalSeconds > MinOnTimeSeconds;

                if (inNeutral && minTimePassed)
                {
                    leftStableFrames++;
                    if (leftStableFrames >= StableFramesRequired)
                    {
                        leftOn = false;
                        Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, veh.Handle, 1, false);
                        ResetLeftState();
                    }
                }
                else if (!inNeutral)
                {
                    leftStableFrames = 0;
                }
            }

            // Правый
            if (rightOn)
            {
                bool inNeutral = Math.Abs(steeringDeg) < NeutralThresholdDeg;
                bool steeringMoving = Math.Abs(steeringSpeed) > SteeringSpeedThreshold;
                if (!inNeutral || steeringMoving)
                    rightWasOutOfNeutral = true;

                bool minTimePassed = (DateTime.Now - rightOnTime).TotalSeconds > MinOnTimeSeconds;

                if (inNeutral && minTimePassed)
                {
                    rightStableFrames++;
                    if (rightStableFrames >= StableFramesRequired)
                    {
                        rightOn = false;
                        Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, veh.Handle, 0, false);
                        ResetRightState();
                    }
                }
                else if (!inNeutral)
                {
                    rightStableFrames = 0;
                }
            }
        }

        DrawIndicators();
    }

    // ---------------------------- Динамическая отрисовка ----------------------------
    private void DrawIndicators()
    {
        if (!leftOn && !rightOn && !hazardOn) return;

        DateTime now = DateTime.Now;

        // ===== ЛЕВЫЙ ИНДИКАТОР =====
        if (leftOn || hazardOn)
        {
            double elapsed = (now - leftStartTime).TotalSeconds;
            double cycleTime = elapsed % FullBlinkCycle;

            if (cycleTime < LightOnDuration)
            {
                int animStep = (int)(cycleTime / StepInterval);

                float baseLeftX = indicatorX - horizontalOffset;
                for (int i = 0; i < 3; i++)
                {
                    if (i <= animStep)
                    {
                        float posX = baseLeftX - (i * arrowSpacing);
                        DrawText("~y~" + LeftArrowChar, posX, indicatorY, indicatorScale);
                    }
                }
            }
        }

        // ===== ПРАВЫЙ ИНДИКАТОР =====
        if (rightOn || hazardOn)
        {
            double elapsed = (now - rightStartTime).TotalSeconds;
            double cycleTime = elapsed % FullBlinkCycle;

            if (cycleTime < LightOnDuration)
            {
                int animStep = (int)(cycleTime / StepInterval);

                float baseRightX = indicatorX + horizontalOffset;
                for (int i = 0; i < 3; i++)
                {
                    if (i <= animStep)
                    {
                        float posX = baseRightX + (i * arrowSpacing);
                        DrawText("~y~" + RightArrowChar, posX, indicatorY, indicatorScale);
                    }
                }
            }
        }
    }

    // ---------------------------- Обработка нажатий ----------------------------
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Modifiers != Keys.None) return;

        Ped player = Game.Player.Character;
        if (!player.IsInVehicle()) return;

        Vehicle veh = player.CurrentVehicle;
        if (veh == null || !veh.Exists()) return;

        switch (e.KeyCode)
        {
            case Keys.D1: // ЛЕВЫЙ ПОВОРОТНИК
                // Если была включена аварийка — гасим ее и правый поворотник
                if (hazardOn)
                {
                    hazardOn = false;
                    Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, veh.Handle, 0, false);
                    ResetRightState();
                }

                if (rightOn)
                {
                    rightOn = false;
                    Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, veh.Handle, 0, false);
                    ResetRightState();
                }

                leftOn = !leftOn;
                Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, veh.Handle, 1, leftOn);

                if (leftOn)
                {
                    leftOnTime = DateTime.Now;
                    leftStartTime = DateTime.Now;
                    leftStableFrames = 0;
                    leftWasOutOfNeutral = false;
                }
                else
                {
                    ResetLeftState();
                }
                break;

            case Keys.D2: // ПРАВЫЙ ПОВОРОТНИК
                // Если была включена аварийка — гасим ее и левый поворотник
                if (hazardOn)
                {
                    hazardOn = false;
                    Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, veh.Handle, 1, false);
                    ResetLeftState();
                }

                if (leftOn)
                {
                    leftOn = false;
                    Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, veh.Handle, 1, false);
                    ResetLeftState();
                }

                rightOn = !rightOn;
                Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, veh.Handle, 0, rightOn);

                if (rightOn)
                {
                    rightOnTime = DateTime.Now;
                    rightStartTime = DateTime.Now;
                    rightStableFrames = 0;
                    rightWasOutOfNeutral = false;
                }
                else
                {
                    ResetRightState();
                }
                break;

            case Keys.D3: // АВАРИЙКА
                hazardOn = !hazardOn;
                DateTime now = DateTime.Now;

                if (hazardOn)
                {
                    leftOn = false;
                    rightOn = false;
                    leftStartTime = now;
                    rightStartTime = now;

                    Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, veh.Handle, 0, true);
                    Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, veh.Handle, 1, true);
                }
                else
                {
                    Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, veh.Handle, 0, false);
                    Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, veh.Handle, 1, false);
                    ResetLeftState();
                    ResetRightState();
                }
                break;
        }
    }

    // ---------------------------- Вспомогательные методы ----------------------------
    private float CalculateSteeringSpeed(float currentSteeringDeg)
    {
        DateTime now = DateTime.Now;
        float deltaTime = (float)(now - lastSteeringTime).TotalSeconds;
        if (deltaTime < 0.001f) return 0f;

        float deltaSteering = currentSteeringDeg - lastSteeringDeg;
        float speed = deltaSteering / deltaTime;

        lastSteeringDeg = currentSteeringDeg;
        lastSteeringTime = now;

        return speed;
    }

    private void ResetLeftState()
    {
        leftOnTime = DateTime.MinValue;
        leftStartTime = DateTime.MinValue;
        leftStableFrames = 0;
        leftWasOutOfNeutral = false;
    }

    private void ResetRightState()
    {
        rightOnTime = DateTime.MinValue;
        rightStartTime = DateTime.MinValue;
        rightStableFrames = 0;
        rightWasOutOfNeutral = false;
    }

    private void ResetStates()
    {
        leftOn = false;
        rightOn = false;
        hazardOn = false;
        ResetLeftState();
        ResetRightState();
        lastSteeringDeg = 0f;
        lastSteeringTime = DateTime.Now;
    }

    // ---------- Отрисовка текста ----------
    private void DrawText(string text, float x, float y, float scale)
    {
        if (string.IsNullOrEmpty(text)) return;

        Function.Call(Hash.SET_TEXT_FONT, 4);
        Function.Call(Hash.SET_TEXT_SCALE, scale, scale);
        Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, 255);
        Function.Call(Hash.SET_TEXT_DROPSHADOW, 1, 0, 0, 0, 255);
        Function.Call(Hash.SET_TEXT_OUTLINE);
        Function.Call(Hash.SET_TEXT_CENTRE, true);
        Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
        Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);
        Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, x, y);
    }
}