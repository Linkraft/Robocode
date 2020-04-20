using Robocode;
using Robocode.Util;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Transactions;

namespace CAP4053.Student 
{
    public class StateMachine 
    {
        public abstract class State 
        {
            public TeamRobot r;
            public bool enemyFound;
            public bool highEnergy;

            protected ScannedRobotEvent lastKnownLocation;
            protected bool fireAtEnemy = true;
            protected double firePower = 3;
            protected double enemyEnergy = 100;
            protected double reliableDistance = 250;
            protected double movementDistance = 35;
            protected double direction = 1;
            protected int initCountdown = 10;
            protected int finalCountdown;

            public abstract void Init(ref TeamRobot robot);
            public abstract void Update();
            public void OnScannedRobotHandler(ScannedRobotEvent e) {
                // Ignore our teammates if scanned
                if (e.Name == "CAP4053.Benchmark.WreckItRalph (1)" || e.Name == "CAP4053.Benchmark.WreckItRalph (2)" || e.Name == "CAP4053.Benchmark.TrevBot (1)" || e.Name == "CAP4053.Benchmark.TrevBot (2)")
                    return;
                
                lastKnownLocation = e;
                enemyFound = true;
                finalCountdown = initCountdown;

                double radarHeading = 1.9 * Utils.NormalRelativeAngle(r.HeadingRadians - r.RadarHeadingRadians + e.BearingRadians);
                double bodyHeading = smallestAngle(r.RadarHeadingRadians - r.HeadingRadians);
                double gunHeading = smallestAngle(r.HeadingRadians - r.GunHeadingRadians + e.BearingRadians);

                // Calculate firing angle using Linear Targeting
                if (fireAtEnemy && r.GunHeat == 0 && System.Math.Abs(r.GunTurnRemaining) < 10)
                {
                    gunHeading = Utils.NormalRelativeAngle(((r.HeadingRadians + e.BearingRadians)
                        + Math.Asin(e.Velocity / (20 - firePower * 3) 
                        * Math.Sin(e.HeadingRadians - (r.HeadingRadians + e.BearingRadians)))
                        - r.GunHeadingRadians));
                    r.SetFire(firePower);
                }

                r.SetTurnRightRadians(direction * (bodyHeading + Math.PI / 2)); // Circle the opponent
                r.SetTurnRadarRightRadians(radarHeading);
                r.SetTurnGunRightRadians(gunHeading);

                // Employ Stop and GO movement to confuse enemy tracking
                if (enemyEnergy > (enemyEnergy = e.Energy)) r.SetAhead(movementDistance);
            }
            public double smallestAngle(double heading) 
            {
                // Calculate the smallest version of an angle (i.e. 270 degrees = -90 degrees)
                double newHeading = heading;
                if (newHeading > Math.PI) newHeading -= (2 * Math.PI);
                else if (newHeading < -Math.PI) newHeading += (2 * Math.PI);
                return newHeading;
            }
            public void HitWallHandler(HitWallEvent e) 
            {
                direction = -direction;
            }
        }

        public List<State> states;
        int input;
        State current;
        Queue<Event> events;
        int[,] transitions; // transition[#s][#i] = #s

        public void Init(TeamRobot robot) 
        {
            // Initialize the starting states
            Scan scan = new Scan();
            scan.Init(ref robot);

            Offensive off = new Offensive();
            off.Init(ref robot);

            Defensive def = new Defensive();
            def.Init(ref robot);

            current = scan;

            // Initialize the event queue for handling events
            events = new Queue<Event>();

            // Initialize and store the references to the states
            states = new List<State>();
            states.Add(current);
            states.Add(off);
            states.Add(def);

            // Make transition table
            transitions = new int[states.Count, states.Count];
            transitions[0, 0] = 0;  // Scan + !enemyFound == Scan
            transitions[0, 1] = 1;  // Scan + highEnergy == Offensive
            transitions[0, 2] = 2;  // Scan + !highEnergy == Defensive
            transitions[1, 0] = 0;  // Offensive + !enemyFound == Scan
            transitions[1, 1] = 1;  // Offensive + highEnergy == Offensive
            transitions[1, 2] = 2;  // Offensive + !highEnergy == Defensive
            transitions[2, 0] = 0;  // Defensive + !enemyFound == Scan
            transitions[2, 1] = 1;  // Defensive + highEnergy == Offensive
            transitions[2, 2] = 2;  // Defensive + !highEnergy == Defensive
        }
        public void Update() 
        {
            // Update the state
            current.Update();

            // Parse any incoming events
            while (events.Count > 0)
            {
                Event e = events.Dequeue();
                if (e is ScannedRobotEvent) current.OnScannedRobotHandler((ScannedRobotEvent)e);
                if (e is HitWallEvent) current.HitWallHandler((HitWallEvent)e);
                UpdateInput();
            }
            // Update the input based on knowledge gained in this update
            UpdateInput();
        }
        public void UpdateInput() 
        {
            // This allows the program to give consideration to two conditions:
            // if the enemy has been found and if health of the player >= 50%
            bool enemyFound = current.enemyFound;
            bool highEnergy = current.r.Energy >= 50;

            if (enemyFound) input = highEnergy ? 1 : 2; // Offensive (1) or Defensive (2)...
            else input = 0; // ...or switch to Scan (0) if we don't know where the enemy is
        }
        public void EnqueueEvent(Event e) {
            events.Enqueue(e); 
        }
        public void Transition() 
        {
            int state = 0;
            for (int i = 0; i < states.Count; i++) 
                if (states[i] == current) state = i;
            current = states[transitions[state, input]];
        }

        public class Scan : State
        {
            public override void Init(ref TeamRobot robot)
            {
                r = robot;
                enemyFound = false;
            }
            public override void Update()
            {
                r.SetTurnRadarRight(45);
            }
        }
        public class Offensive : State
        {
            public override void Init(ref TeamRobot robot) 
            {
                r = robot;
                finalCountdown = initCountdown;
                enemyFound = true;
            }
            public override void Update() 
            {
                // Employ countdown to go back to Scan if we lost track of the enemy
                finalCountdown -= 1;
                if (finalCountdown == 0) enemyFound = false;
            }
        }
        public class Defensive : State
        {
            public override void Init(ref TeamRobot robot) 
            {
                r = robot;
                enemyFound = true;
            }
            public override void Update() 
            {
                if (lastKnownLocation != null) 
                {
                    // Fire at enemy only if we can reliably hit them
                    if (lastKnownLocation.Distance >= reliableDistance) fireAtEnemy = false;
                    else
                    {
                        fireAtEnemy = true;
                        // Conserve energy by basing fire power off of how far away the enemy is
                        firePower = Math.Min(r.Energy, Math.Min(3, reliableDistance / lastKnownLocation.Distance));
                    }
                }
                // Employ countdown to go back to Scan if we lost track of the enemy
                finalCountdown -= 1;
                if (finalCountdown == 0) enemyFound = false;
            }
        }
    }
    public class WreckItRalph : TeamRobot
    {
        private StateMachine fsm;
        public override void Run() 
        {
            // Set team colors
            SetColors(Color.Orange, Color.DarkOrange, Color.OrangeRed, Color.DarkOrange, Color.Orange);

            // In the words of ConnorJC, this engages "non-shit mode"
            IsAdjustGunForRobotTurn = true;
            IsAdjustRadarForGunTurn = true;

            // Let the Finite State Machine handle the rest
            fsm = new StateMachine();
            fsm.Init(this);
            while (true) 
            {
                fsm.Update();
                Execute();
                fsm.Transition();
            }
        }
        public override void OnScannedRobot(ScannedRobotEvent evnt) 
        {
            fsm.EnqueueEvent(evnt);
        }
        public override void OnHitWall(HitWallEvent evnt) 
        {
            fsm.EnqueueEvent(evnt);
        }
    }
}
