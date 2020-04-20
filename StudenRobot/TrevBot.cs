using Robocode;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace CAP4053.Student
{
    public class FSM
    {
        List<State> states;
        public State current;
        public State start;
        int[,] transition = new int[3,4]; //number of states and number of inputs
        public FSM(List<State> states)
        {
            this.states = states;
        }

        public void Init()
        {
            this.start = states[0];
            this.current = states[0];

            //[currentstate, input] = nextstate;
            transition[0,0] = 0;
            transition[1,0] = 2;
            transition[2,0] = 2;

            transition[0,1] = 1;
            transition[1,1] = 2;
            transition[2,1] = 2;

            transition[0,2] = 0;
            transition[1,2] = 0;
            transition[2,2] = 0;
        }
        public void Update()
        {
            current.update();
        }
        
        public void Transition()
        {
            int state = 0;
            int input = 0;
            while(state < states.Count)
            {
                if(states[state] == current)
                {
                    break;
                }
                state++;
            }
            // if(current.enemyDeath == true)
            // {
            //     input = 3;
            // }
            if(current.wrongTeam == true) //priority over robotScanned
            {
                input = 2;
            }
            else if(current.robotScanned == true)
            {
                input = 1;
            }
            current = states[transition[state, input]];
        }

        public void Exit()
        {
            current.exit();
        }
    }

    public abstract class State
    {
        TeamRobot robot;
        double enemyBearing = 0;
        String enemyName = " ";
        bool robotHit = false;
        String newName1 = "CAP4053.Student.TrevBot (1)";
        String newName2 = "CAP4053.Student.TrevBot (2)";
        String newName3 = "CAP4053.Student.WreckItRalph (1)";
        String newName4 = "CAP4053.Student.WreckItRalph (2)";

        public bool robotScanned = false;
        public bool wrongTeam = false;
        // public bool enemyDeath = false;
        public State(TeamRobot robot)
        {
            this.robot = robot;
        }

        public void getRobotScannedEvent(ScannedRobotEvent evnt)
        {
            enemyBearing = evnt.Bearing;
            enemyName = evnt.Name;

            if(evnt.Name == newName1 || evnt.Name == newName2 || evnt.Name == newName3 || evnt.Name == newName4 ) 
            {
                wrongTeam = true;
                return;
            }
            double gunAdjustForTarget = 0;

            robot.SetStop();
            double radarAdjustment = (evnt.Bearing - robot.RadarHeading) + robot.Heading;
            if(radarAdjustment > 180)
            {
                radarAdjustment -= 360;
            }
            if(radarAdjustment < -180)
            {
                radarAdjustment += 360;
            }

            if(robotHit == true)
            {
                double enemyX = evnt.Distance * Math.Sin(evnt.BearingRadians + robot.HeadingRadians) + robot.X;
                double enemyY = evnt.Distance * Math.Cos(evnt.BearingRadians + robot.HeadingRadians) + robot.Y;
                double timeToShoot = evnt.Distance / 5;
                double distanceEnemyTravels = timeToShoot * evnt.Velocity;

                double destinationX = distanceEnemyTravels * Math.Sin(evnt.HeadingRadians) + enemyX;
                double destinationY = distanceEnemyTravels * Math.Cos(evnt.HeadingRadians) + enemyY;
                double a = Math.Sqrt(Math.Pow(enemyX - destinationX, 2) + Math.Pow(enemyY - destinationY, 2));
                double b = Math.Sqrt(Math.Pow(robot.X - destinationX, 2) + Math.Pow(robot.Y - destinationY, 2));
                double c = Math.Sqrt(Math.Pow(robot.X - enemyX, 2) + Math.Pow(robot.Y - enemyY, 2));

                gunAdjustForTarget = (180/Math.PI) * Math.Acos( (Math.Pow(b,2) + Math.Pow(c,2) - Math.Pow(a,2)) / (2 * b * c) );
            }
            double gunAdjustment = (evnt.Bearing - robot.GunHeading) + robot.Heading + gunAdjustForTarget;
            if(gunAdjustment > 180)
            {
                gunAdjustment -= 360;
            }
            if(gunAdjustment < -180)
            {
                gunAdjustment += 360;
            }
            robot.SetTurnRadarRight(radarAdjustment);
            robot.SetTurnGunRight(gunAdjustment);
            robotScanned = true;
        }

        public void getOnHitRobot(HitRobotEvent evnt)
        {
            robotHit = true;
            if(robot.GunHeat == 0)
            {
                robot.SetFire(Rules.MAX_BULLET_POWER);
            }
            robot.SetAhead(100);
        }

        abstract public void update();
        public void exit()
        {
            robotScanned = false;
            wrongTeam = false;
            // enemyDeath = false;
        }
    }

    public class SitAndScan : State
    {
        TeamRobot robot;
        public SitAndScan(TeamRobot robot) : base(robot)
        {
            this.robot = robot;
        }
        public override void update()
        {
            robot.SetTurnRadarRight(1000);
        }
    }

    public class HeadToRobot : State
    {
        TeamRobot robot;

        public HeadToRobot(TeamRobot robot) : base(robot)
        {
            this.robot = robot;
        }

        public override void update()
        {
            double moveAdust = robot.RadarHeading - robot.Heading;
            if(moveAdust > 180)
            {
                moveAdust -= 360;
            }
            if(moveAdust < -180)
            {
                moveAdust += 360;
            }
            robot.SetTurnRight(moveAdust);
            robot.SetAhead(250);
        }
    }

    public class ShootAndMove : State
    {
        TeamRobot robot;
        public ShootAndMove(TeamRobot robot) : base(robot)
        {
            this.robot = robot;
        }

        public override void update()
        {
            if(robot.GunHeat == 0)
            {
                robot.SetFire(Rules.MAX_BULLET_POWER);
            }
            else
            {
                double moveAdust = robot.RadarHeading - robot.Heading;
                if(moveAdust > 180)
                {
                    moveAdust -= 360;
                }
                if(moveAdust < -180)
                {
                    moveAdust += 360;
                }
                robot.SetTurnRight(moveAdust);
                robot.SetAhead(0);
                // if(enemyBearing < 75 && enemyBearing > -75) //enemy in front of us
                // {
                    robot.SetAhead(250);
                // }
            }
        }
    }

    public class TrevBot : TeamRobot
    {
        SitAndScan SitAndScan;
        HeadToRobot HeadToRobot;
        ShootAndMove ShootAndMove;
        List<State> states = new List<State>();
        FSM fsm;
        public override void Run()
        {
            SitAndScan = new SitAndScan(this);
            HeadToRobot = new HeadToRobot(this);
            ShootAndMove = new ShootAndMove(this);
            states.Add(SitAndScan);
            states.Add(HeadToRobot);
            states.Add(ShootAndMove);
            fsm = new FSM(states);
            SetColors(Color.Orange, Color.DarkOrange, Color.OrangeRed, Color.DarkOrange, Color.Orange);
            IsAdjustRadarForGunTurn = true;
            IsAdjustRadarForRobotTurn = true;
            fsm.Init();
            while(true)
            {
                fsm.Update();
                Execute();
                fsm.Transition();
                fsm.Exit();
            }
        }
        
        //events
        public override void OnScannedRobot(ScannedRobotEvent evnt)
        {
            fsm.current.getRobotScannedEvent(evnt);
        }

        public override void OnHitRobot(HitRobotEvent evnt)
        {
            fsm.current.getOnHitRobot(evnt);
        }

    }
}