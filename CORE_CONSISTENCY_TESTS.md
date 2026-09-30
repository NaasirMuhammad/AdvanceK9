# AdvancedK9 Build 867 — core consistency checks

Internal version 0.24.0.81. This is a focused test build after the released v0.24.0 beta baseline; it is not yet certified in game.

1. Start an area, vehicle and building search. During navigation, sniffing and the final indication, issue Follow, Stay, Sit, then a different search. The latest accepted task must remain in control; old workers must not restore a previous route, sit or alert later.
2. Start a track and interrupt it with Sit, Stay, Recall, a search and Enter Vehicle. Repeat while Rex is turning or checking direction. Test Inspect, camera and EMS requests during tracking: these must leave it active.
3. Sit -> Follow/Recall must finish sit_exit before walking. Lie -> Follow must finish getup_l/getup_r before moving. Sit -> Lie remains the existing direct transition. Idle variation must not restart a sit/get-up transition.
4. Issue Sit, Recall and Sit rapidly. Foreground actions execute one at a time and only the latest pending request survives; Rex must not overlap animations.
5. Enter/exit the tested sedan and SUV, then deploy/dismiss at both kennel types. Queue Recall during a transition. Check door, collision, height, frozen state and final positioning against the previous working build.
6. Reject a command through medical eligibility or hesitation while Rex is doing other work. His current task must remain valid. Confirm apprehend still works from keyboard, controller and voice.
7. Go off duty or unload during search, track and a transition. No old worker should task a replacement/deleted dog or resume after redeployment.

Record the command sequence, vehicle/dog model, visible result and RagePluginHook.log. Look for the task-owner-superseded message on interruptions. Windows compilation alone does not validate animation appearance or native timing.
