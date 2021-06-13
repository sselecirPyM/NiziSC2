using NiziSC2.Core;
using SC2APIProtocol;
using System;
using System.IO;
using System.Xml.Serialization;

namespace NiziSC2Bot1
{
    class Program
    {
        static void Main(string[] args)
        {
            GameContext gameContext = new GameContext();
            if (args.Length == 0)
            {
                LocalGame(gameContext);

                Run(gameContext);

                gameContext.gameConnection.LeaveGame();
            }
        }

        static void Run(GameContext gameContext)
        {
            gameContext.GameInitialize();

            System.Diagnostics.Debug.WriteLine(string.Format("Map:{0}", gameContext.gameInfo.MapName), "Nizi");

            bot = new Bot();
            bot.gameContext = gameContext;
            bool initialized = false;
            while (true)
            {
                var response = gameContext.gameConnection.RequestObservation();
                gameContext.observation = response.Observation;

                if (response.Status == Status.Ended || response.Status == Status.Quit)
                {
                    foreach (var result in response.Observation.PlayerResult)
                    {
                        if (result.PlayerId == gameContext.playerId)
                            Console.WriteLine("Result: {0}", result.Result);
                    }
                    break;
                }
                gameContext.FrameBegin();
                if (!initialized)
                {
                    bot.Initialize();
                    initialized = true;
                }

                var actions = bot.Work();

                gameContext.gameConnection.RequestAction(actions.actions);
                gameContext.FrameEnd();
            }
        }
        static Bot bot;
        static void LadderGame(string[] args, GameContext gameContext)
        {
            gameContext.JoinGameLadder(Race.Terran, 0);
        }
        static void LocalGame(GameContext gameContext)
        {
            var serializer = new XmlSerializer(typeof(LaunchOptions));
            var launchOptions = (LaunchOptions)serializer.Deserialize(File.OpenRead("Data/LaunchOptions.xml"));
            gameContext.launchOptions = launchOptions;

            SC2GameHelp.LaunchSC2(launchOptions.Port, out var starcraftMaps);
            gameContext.ConnectToGame(launchOptions.Port);

            var player2 = new PlayerSetup
            {
                Race = launchOptions.OpponentRace,
                Type = PlayerType.Computer,
                Difficulty = launchOptions.OpponentDifficulty
            };
            gameContext.gameConnection.NewGame(player2, Path.Combine(starcraftMaps, launchOptions.Map));

            gameContext.JoinGame(launchOptions.OurRace);
        }
    }
}
