using System;
using System.Windows.Forms;
using GTA;
using GTA.Native;

public class GTA5E_TurnSignals : Script
{
    private bool leftOn = false;
    private bool rightOn = false;
    private bool hazardOn = false;

    private int lastVehicleHandle = 0; // Отслеживание текущего ТС

    public GTA5E_TurnSignals()
    {
        this.KeyDown += OnKeyDown;
        this.Tick += OnTick;
        this.Interval = 500; // Проверка смены машины 2 раза в секунду
    }

    private void OnTick(object sender, EventArgs e)
    {
        Ped player = Game.Player.Character;

        // Если игрок пешком — сбрасываем состояние
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

        // Если игрок пересел в другое авто — сбрасываем состояние
        if (veh.Handle != lastVehicleHandle)
        {
            ResetStates();
            lastVehicleHandle = veh.Handle;
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // Игнорируем нажатие, если зажаты Alt, Ctrl или Shift (например, при смене языка)
        if (e.Modifiers != Keys.None) return;

        Ped player = Game.Player.Character;
        if (!player.IsInVehicle()) return;

        Vehicle veh = player.CurrentVehicle;
        if (veh == null || !veh.Exists()) return;

        switch (e.KeyCode)
        {
            case Keys.D1: // Левый поворотник
                if (hazardOn) break;

                if (rightOn)
                {
                    rightOn = false;
                    Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, veh.Handle, 0, false);
                }

                leftOn = !leftOn;
                Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, veh.Handle, 1, leftOn);
                break;

            case Keys.D2: // Правый поворотник
                if (hazardOn) break;

                if (leftOn)
                {
                    leftOn = false;
                    Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, veh.Handle, 1, false);
                }

                rightOn = !rightOn;
                Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, veh.Handle, 0, rightOn);
                break;

            case Keys.D3: // Аварийка
                hazardOn = !hazardOn;
                if (hazardOn)
                {
                    leftOn = true;
                    rightOn = true;
                    Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, veh.Handle, 0, true);
                    Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, veh.Handle, 1, true);
                }
                else
                {
                    leftOn = false;
                    rightOn = false;
                    Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, veh.Handle, 0, false);
                    Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, veh.Handle, 1, false);
                }
                break;
        }
    }

    private void ResetStates()
    {
        leftOn = false;
        rightOn = false;
        hazardOn = false;
    }
}