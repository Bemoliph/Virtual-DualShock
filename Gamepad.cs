using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using vJoyInterfaceWrap;

namespace VirtualDualShock
{
    public class Gamepad
    {
        private readonly vJoy vjoy;
        public readonly uint vjoyID;
        private readonly uint dpadID = 1;

        private static readonly uint minDevice = 1;
        private static readonly uint maxDevice = 16;

        private Random rng;

        public Gamepad()
        {
            vjoy = new vJoy();

            if (!vjoy.vJoyEnabled())
            {
                throw new System.IO.IOException("Failed to get vJoy device, driver not enabled?");
            }

            vjoyID = findAvailableDeviceID();
            bool gotDevice = vjoy.AcquireVJD(vjoyID);

            if (!gotDevice)
            {
                throw new System.IO.IOException("Failed to get vJoy device " + vjoyID);
            }

            releaseAll();

            rng = new Random();
        }

        private uint findAvailableDeviceID()
        {
            int busy = 0;
            int disabled = 0;

            for (uint i = minDevice; i <= maxDevice; i++)
            {
                VjdStat status = vjoy.GetVJDStatus(i);

                switch (status)
                {
                    case VjdStat.VJD_STAT_OWN:
                    case VjdStat.VJD_STAT_FREE:
                        if (hasCorrectInputs(i))
                        {
                            return i;
                        }
                        break;
                    case VjdStat.VJD_STAT_BUSY:
                        busy++;
                        break;
                    case VjdStat.VJD_STAT_MISS:
                        disabled++;
                        break;
                }
            }

            throw new System.IO.IOException("Failed to get vJoy device, " + busy + " busy and " + disabled + "disabled out of " + maxDevice + " devices.");
        }

        private bool hasCorrectInputs(uint deviceID)
        {
            bool hasEnoughButtons = 12 <= vjoy.GetVJDButtonNumber(deviceID);

            bool hasDirectional = 1 <= vjoy.GetVJDDiscPovNumber(deviceID);

            bool hasAxisX = vjoy.GetVJDAxisExist(deviceID, HID_USAGES.HID_USAGE_X);
            bool hasAxisY = vjoy.GetVJDAxisExist(deviceID, HID_USAGES.HID_USAGE_Y);
            bool hasAxisZ = vjoy.GetVJDAxisExist(deviceID, HID_USAGES.HID_USAGE_Z);
            bool hasAxisRZ = vjoy.GetVJDAxisExist(deviceID, HID_USAGES.HID_USAGE_RZ);
            bool hasCorrectAxes = hasAxisX && hasAxisY && hasAxisZ && hasAxisRZ;

            return hasEnoughButtons && hasDirectional && hasCorrectAxes;
        }

        private int getTapLength(int milliseconds, int variance)
        {
            return milliseconds - (int)(variance * rng.NextDouble());
        }

        public void tapButton(Button button, int milliseconds = 30, int variance = 5)
        {
            holdButton(button);
            System.Threading.Thread.Sleep(getTapLength(milliseconds, variance));
            releaseButton(button);
        }

        public void holdButton(Button button)
        {
            vjoy.SetBtn(true, vjoyID, (uint)button);
        }

        public void releaseButton(Button button)
        {
            vjoy.SetBtn(false, vjoyID, (uint)button);
        }

        public void tapDirectional(Directional directional, int milliseconds = 30, int variance = 5)
        {
            holdDirectional(directional);
            System.Threading.Thread.Sleep(getTapLength(milliseconds, variance));
            releaseDirectional();
        }

        public void holdDirectional(Directional directional)
        {
            vjoy.SetDiscPov((int)directional, vjoyID, dpadID);
        }

        public void releaseDirectional()
        {
            vjoy.SetDiscPov((int)Directional.None, vjoyID, dpadID);
        }

        public void holdStick(StickState state)
        {
            switch (state.stick)
            {
                case Stick.Left:
                    vjoy.SetAxis(state.getHorizontalAxis(), vjoyID, HID_USAGES.HID_USAGE_X);
                    vjoy.SetAxis(state.getVerticalAxis(), vjoyID, HID_USAGES.HID_USAGE_Y);
                    break;
                case Stick.Right:
                    vjoy.SetAxis(state.getHorizontalAxis(), vjoyID, HID_USAGES.HID_USAGE_Z);
                    vjoy.SetAxis(state.getVerticalAxis(), vjoyID, HID_USAGES.HID_USAGE_RZ);
                    break;
            }
        }

        public void releaseStick(Stick stick)
        {
            holdStick(new StickState(stick, 0.0, 0.0));
        }

        public void releaseAll()
        {
            foreach (Button button in Enum.GetValues(typeof(Button)))
            {
                releaseButton(button);
            }

            releaseDirectional();

            foreach (Stick stick in Enum.GetValues(typeof(Stick)))
            {
                releaseStick(stick);
            }
        }
    }

    public class InputSequence
    {
        private List<InputFrame> sequence;
        protected int sequenceDuration;

        public InputSequence()
        {
            sequence = new List<InputFrame>();
        }

        public void addWait(int duration)
        {
            sequenceDuration += duration;
        }

        public void addButton(Button button, int duration, bool isBlocking = false)
        {
            sequence.Add(new ButtonFrame(button, true, sequenceDuration++));
            sequence.Add(new ButtonFrame(button, false, sequenceDuration++ + duration));

            if (isBlocking)
            {
                addWait(duration);
            }
        }

        public void addDirectional(Directional directional, int duration, bool isBlocking = false)
        {
            sequence.Add(new DirectionalFrame(directional, true, sequenceDuration++));
            sequence.Add(new DirectionalFrame(directional, false, sequenceDuration++ + duration));

            if (isBlocking)
            {
                addWait(duration);
            }
        }

        public void addStickState(StickState state, int duration, bool isBlocking = false)
        {
            sequence.Add(new StickFrame(state, true, sequenceDuration++));

            if (isBlocking)
            {
                addWait(duration);
            }
        }

        public int getDuration()
        {
            return sequenceDuration;
        }

        public void parseSequence(string inputSequence, int defaultDuration = 30, int paddingDuration = 15)
        {

        }

        public void run(Gamepad gamepad, bool clearAtStart = true, bool clearAtEnd = true)
        {
            int playbackTime = 0;
            var timeline = sequence.OrderBy(o => o.timestamp);

            if (clearAtStart)
            {
                gamepad.releaseAll();
            }

            foreach (InputFrame frame in timeline)
            {
                int timeToInput = frame.timestamp - playbackTime;
                System.Threading.Thread.Sleep(timeToInput);
                playbackTime += timeToInput;

                frame.applyInput(gamepad);
            }

            int timeToEnd = sequenceDuration - playbackTime;
            if (0 < timeToEnd)
            {
                System.Threading.Thread.Sleep(timeToEnd);
            }

            if (clearAtEnd)
            {
                gamepad.releaseAll();
            }
        }

        private abstract class InputFrame
        {
            protected bool push;
            public int timestamp;

            public abstract void applyInput(Gamepad gamepad);
        }

        private class ButtonFrame : InputFrame
        {
            private Button button;

            public ButtonFrame(Button button, bool push, int timestamp)
            {
                this.button = button;
                this.push = push;
                this.timestamp = timestamp;
            }

            override public void applyInput(Gamepad gamepad)
            {
                if (push)
                {
                    gamepad.holdButton(button);
                }
                else
                {
                    gamepad.releaseButton(button);
                }
            }
        }

        private class DirectionalFrame : InputFrame
        {
            private Directional directional;

            public DirectionalFrame(Directional directional, bool push, int timestamp)
            {
                this.directional = directional;
                this.push = push;
                this.timestamp = timestamp;
            }

            override public void applyInput(Gamepad gamepad)
            {
                if (push)
                {
                    gamepad.holdDirectional(directional);
                }
                else
                {
                    gamepad.releaseDirectional();
                }
            }
        }

        private class StickFrame : InputFrame
        {
            private StickState state;

            public StickFrame(StickState state, bool push, int timestamp)
            {
                this.state = state;
                this.push = push;
                this.timestamp = timestamp;
            }

            override public void applyInput(Gamepad gamepad)
            {
                if (push)
                {
                    gamepad.holdStick(state);
                }
                else
                {
                    gamepad.releaseStick(state.stick);
                }
            }
        }
    }

    public class StickState
    {
        private static int axisMax = 32767;

        public readonly Stick stick;
        private double x;
        private double y;

        public StickState(Stick stick, double x, double y)
        {
            Contract.Requires(-1.0 <= x && x <= 1.0);
            Contract.Requires(-1.0 <= y && y <= 1.0);

            this.stick = stick;
            this.x = x;
            this.y = y;
        }

        private int scaleToAxis(double a)
        {
            return (int)(((a + 1.0) / 2.0) * axisMax);
        }

        public int getHorizontalAxis(bool inverted = false)
        {
            return scaleToAxis(inverted ? -x : x);
        }

        public int getVerticalAxis(bool inverted = true)
        {
            return scaleToAxis(inverted ? -y : y);
        }
    }

    public enum Button
    {
        Square = 1,
        Triangle = 2,
        Cross = 3,
        Circle = 4,

        L1 = 5,
        R1 = 6,

        L2 = 7,
        R2 = 8,

        L3 = 9,
        R3 = 10,

        Select = 11,
        Start = 12
    }

    public enum Directional
    {
        None = -1,

        Up = 0,
        Right = 1,
        Down = 2,
        Left = 3
    }

    public enum Stick
    {
        Left,
        Right
    }
}
