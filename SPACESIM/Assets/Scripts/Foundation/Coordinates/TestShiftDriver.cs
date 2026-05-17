// MOVED TO SimTick/ MODULE IN COMMIT 033
// This file's content has been moved to SPACESIM/Assets/Scripts/Foundation/SimTick/TestShiftDriver.cs.
// The Coordinates asmdef cannot reference SimTick (would create a circular dependency:
// SimTick already references Coordinates), so the bridging test driver belongs in the
// SimTick assembly.
//
// This stub file exists only because the Cowork sandbox cannot unlink files that existed
// before the session. Replay procedure runs `git rm` to remove this file from version
// control on the host filesystem where sandbox restrictions don't apply.
//
// The replacement file at SimTick/TestShiftDriver.cs has the same class name and same
// public API as the original, so scene references will resolve correctly after Unity
// reimports — Unity matches by class name + namespace, and the namespace has changed
// from SpaceSim.Foundation.Coordinates to SpaceSim.Foundation.SimTick. The user must
// re-attach the TestShiftDriver component in TestCoordinates.unity per the commit 033
// artifact's user-side verification steps.
