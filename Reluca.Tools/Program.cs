// See https://aka.ms/new-console-template for more information
using Reluca.Tools;

switch (args[0])
{
    case "FeaturePatternCreator":
        FeaturePatternCreator.Execute();
        break;
    case "ScoreFileAdjuster":
        ScoreFileAdjuster.Adjust();
        break;
    case "ValidStateExtractor":
        new ValidStateExtractor().Extract();
        break;
    default:
        Console.WriteLine("想定外の引数です。");
        break;
}
