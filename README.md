# Virtual DualShock

The Virtual DualShock is a basic wrapper around [vJoy](http://vjoystick.sourceforge.net/site/) to simplify using a vJoy device as a PlayStation style gamepad in any XInput-enabled application.  This means having a fully programmable gamepad in any recent game that works with a "real" controller.

The `Gamepad` class requires a free vJoy device with 12 Buttons, 1 D-Pad, and X + Y + Z + rZ axes, and can be manipulated directly with various methods:

    Gamepad gamepad = new Gamepad();
    
    // Press R2 + X
    gamepad.holdButton(Button.R2);
    gamepad.tapButton(Button.Cross);
    gamepad.releaseButton(Button.R2);
    
    // Push left stick all the way forward
    StickState state = new StickState(Stick.Left, 0.0, 1.0);
    // Push right stick to bottom left
    StickState state = new StickState(Stick.Right, -1.0, -1.0);
    
    // Release all inputs back to neutral state
    gamepad.releaseAll();

Alternatively, an `InputSequence` can be built and "played" through an existing `Gamepad` all at once.  This helps simplify button timing and can be useful for storing or repeating sequences later:

    InputSequence seq = new InputSequence();
    bool isBlocking = true; // Force subsequent button presses to come after this press's duration
    
    // Press R2 + X over 40 ms
    seq.addButton(Button.R2, 40, !isBlocking);   // 0  ms: Hold R2 for 40 ms but still allow other inputs
    seq.addWait(10);                             // 0  ms: Wait 10 ms from the start of holding R2
    seq.addButton(Button.Cross, 20, isBlocking); // 10 ms: Tap X over 20 ms with an implied wait of 20 ms
                                                 // 30 ms: Wait 10 ms from the end of tapping X
                                                 // 40 ms: Release R2
    
    seq.run(gamepad);
