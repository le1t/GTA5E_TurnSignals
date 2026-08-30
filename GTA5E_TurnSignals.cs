using System;
using System.Windows.Forms;
using GTA;
using GTA.Native;

public class GTA5E_TurnSignals : Script
{
    // ==================== Состояния поворотников ====================
    private bool leftOn = false;          // Включен ли левый поворотник
    private bool rightOn = false;         // Включен ли правый поворотник
    private bool hazardOn = false;        // Включена ли аварийная сигнализация

    // ==================== Переменные для синхронизации ====================
    private DateTime leftStartTime = DateTime.MinValue;   // Время начала текущего цикла мигания левого (для анимации)
    private DateTime rightStartTime = DateTime.MinValue;  // Время начала текущего цикла мигания правого (для анимации)

    // ==================== Переменные для автоотключения ====================
    private DateTime leftOnTime = DateTime.MinValue;      // Момент включения левого (для проверки минимального времени)
    private DateTime rightOnTime = DateTime.MinValue;     // Момент включения правого (для проверки минимального времени)
    private int leftStableFrames = 0;                     // Счётчик кадров нейтрали для левого (автоотключение)
    private int rightStableFrames = 0;                    // Счётчик кадров нейтрали для правого (автоотключение)
    private bool leftWasOutOfNeutral = false;             // Был ли левый вне нейтрали после включения
    private bool rightWasOutOfNeutral = false;            // Был ли правый вне нейтрали после включения

    // ==================== НАСТРОЙКИ ====================
    private const float NeutralThresholdDeg = 20.0f;          // Порог угла руля для считания "нейтралью" (градусы)
    private const int StableFramesRequired = 4;               // Сколько кадров руль должен быть в нейтрали для автоотключения
    private const float MinOnTimeSeconds = 2.5f;              // Минимальное время работы поворотника перед автоотключением (сек)
    private const float MinSpeedKmh = 5.0f;                   // Минимальная скорость для работы автоотключения (км/ч)
    private const float SteeringSpeedThreshold = 15.0f;       // Порог скорости вращения руля (градус/сек) для выхода из нейтрали

    // ==================== НАСТРОЙКИ СИНХРОНИЗАЦИИ ФАР ====================
    private const float FullBlinkCycle = 0.90f;               // Полный цикл мигания (свет + пауза) в секундах
    private const float LightOnDuration = 0.45f;              // Длительность свечения в цикле (сек)
    private const float StepInterval = 0.15f;                 // Задержка между появлением стрелок в анимации (сек)

    private const string LeftArrowChar = "«";                 // Символ левой стрелки
    private const string RightArrowChar = "»";                // Символ правой стрелки

    // ==================== Служебные переменные ====================
    private float lastSteeringDeg = 0f;                       // Предыдущий угол руля для вычисления скорости поворота
    private DateTime lastSteeringTime = DateTime.Now;         // Время последнего замера угла руля
    private int lastVehicleHandle = 0;                        // Handle последнего автомобиля для отслеживания смены ТС

    // ==================== КООРДИНАТЫ И РАЗМЕР ====================
    private readonly float indicatorX = 0.50f;                // Центр экрана по X (0..1)
    private readonly float indicatorY = 0.90f;                // Позиция по Y (внизу экрана)
    private readonly float horizontalOffset = 0.035f;         // Смещение стрелок от центра влево/вправо
    private readonly float arrowSpacing = 0.012f;              // Расстояние между стрелками в анимации
    private readonly float indicatorScale = 0.75f;            // Размер шрифта стрелок

    // ========================================================================
    public GTA5E_TurnSignals()
    {
        this.KeyDown += OnKeyDown;
        this.Tick += OnTick;
        this.Interval = 0;
    }

    // ---------------------------- Вспомогательные методы ----------------------------
    private bool IsPlayerDriver(Ped player)
    {
        if (!player.IsInVehicle()) return false;
        Vehicle veh = player.CurrentVehicle;
        if (veh == null || !veh.Exists()) return false;

        return veh.GetPedOnSeat(VehicleSeat.Driver) == player;
    }

    private bool IsFirstPersonCamera()
    {
        // View Mode = 4 соответствует виду от первого лица
        return Function.Call<int>(Hash.GET_FOLLOW_PED_CAM_VIEW_MODE) == 4;
    }

    // ---------------------------- Основной цикл ----------------------------
    private void OnTick(object sender, EventArgs e)
    {
        Ped player = Game.Player.Character;

        // Если игрок не в машине или не за рулем — сбрасываем состояние и выходим
        if (!IsPlayerDriver(player))
        {
            if (lastVehicleHandle != 0)
            {
                ResetStates();
                lastVehicleHandle = 0;
            }
            return;
        }

        Vehicle veh = player.CurrentVehicle;

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
        
        // Скрываем GUI-индикаторы при виде от первого лица
        if (IsFirstPersonCamera()) return;

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
        if (!IsPlayerDriver(player)) return;

        Vehicle veh = player.CurrentVehicle;

        switch (e.KeyCode)
        {
            case Keys.D1: // ЛЕВЫЙ ПОВОРОТНИК
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