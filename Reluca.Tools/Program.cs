// See https://aka.ms/new-console-template for more information
using Reluca.Tools.Creators;

Console.WriteLine(args);

switch (args[0])
{
    case "FeaturePatternCreator":
        FeaturePatternCreator.Execute();
        break;
    default:
        Console.WriteLine("想定外の引数です。");
        break;
}
