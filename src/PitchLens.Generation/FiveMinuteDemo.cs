using PitchLens.Core;

namespace PitchLens.Generation;

public static class FiveMinuteDemo
{
    public static MatchPlan Plan()
    {
        const string a1 = "arsenal-raya", a2 = "arsenal-saliba", a3 = "arsenal-odegaard", a4 = "arsenal-rice",
            a5 = "arsenal-saka", a6 = "arsenal-gyokeres", c2 = "manchester-city-dias", c3 = "manchester-city-foden",
            c4 = "manchester-city-cherki", c5 = "manchester-city-haaland", c6 = "manchester-city-doku";
        PlannedAction A(string id, string kind, double start, double end, string actor, string? receiver,
            double x, double y, bool? complete = null) => new(id, kind, start, end, actor, receiver, complete, new(x, y));
        PlanPoint[] Shape(params double[] xy) => Enumerable.Range(0, 5).Select(i => new PlanPoint(xy[i * 2], xy[i * 2 + 1])).ToArray();
        PlannedPhase Phase(int n, string kind, PlanPoint[] arsenal, PlanPoint[] city, params PlannedAction[] actions) =>
            new("phase-" + (n + 1), kind, n * 30, (n + 1) * 30, arsenal, city, actions);
        return new(300, "A Game of Transitions", a1,
        [
            Phase(0,"turnover", Shape(23,18,40,32,34,51,60,12,68,43),Shape(76,20,60,49,55,30,35,40,64,10),
                A("a01","pass",1,4,a1,a2,25,20,true), A("a02","carry",5,10,a2,null,38,24),
                A("a03","pass",11,14,a2,a3,48,30,true), A("a04","carry",15,21,a3,null,63,27),
                A("a05","pass",23,26,a3,c2,68,24,false), A("a06","carry",27,30,c2,null,62,23)),
            Phase(1,"counterattack",Shape(35,18,55,30,44,53,72,12,78,44),Shape(68,24,52,49,47,30,28,42,59,10),
                A("a07","pass",31,34,c2,c4,48,30,true), A("a08","carry",35,40,c4,null,35,27),
                A("a09","pass",41,44,c4,c6,29,13,true), A("a10","carry",45,50,c6,null,17,20),
                A("a11","pass",51,54,c6,a4,31,42,false), A("a12","carry",55,60,a4,null,43,44)),
            Phase(2,"turnover",Shape(27,18,44,31,38,50,59,12,65,43),Shape(60,20,43,50,37,30,20,41,35,12),
                A("a13","pass",61,64,a4,a5,62,13,true), A("a14","carry",65,70,a5,null,76,17),
                A("a15","pass",71,74,a5,a6,82,36,true), A("a16","carry",75,80,a6,null,92,33),
                A("a17","pass",82,85,a6,c2,74,24,false), A("a18","carry",86,90,c2,null,63,25)),
            Phase(3,"finish",Shape(38,18,58,30,52,52,77,14,83,42),Shape(73,24,56,50,49,31,32,41,52,12),
                A("a19","pass",91,94,c2,c3,49,46,true), A("a20","carry",95,100,c3,null,34,44),
                A("a21","pass",101,104,c3,c5,22,34,true), A("a22","carry",105,109,c5,null,12,34),
                A("a23","shot",110,112,c5,null,0,34), A("a24","kickoff",113,116,a3,null,52.5,34),
                A("a25","pass",117,120,a3,a4,49,49,true)),
            Phase(4,"response",Shape(24,18,43,31,39,50,58,11,68,42),Shape(67,20,52,49,46,29,29,40,51,11),
                A("a26","carry",121,126,a4,null,61,49), A("a27","pass",127,130,a4,a5,72,17,true),
                A("a28","carry",131,136,a5,null,88,20), A("a29","pass",137,140,a5,a6,93,34,true),
                A("a30","shot",141,144,a6,null,105,34), A("a31","kickoff",145,148,c3,null,52.5,34),
                A("a32","pass",149,150,c3,c4,47,26,true)),
            Phase(5,"turnover",Shape(40,20,60,32,52,52,78,13,84,44),Shape(74,24,53,48,45,27,30,41,56,11),
                A("a33","carry",151,156,c4,null,34,24), A("a34","pass",157,160,c4,c6,28,12,true),
                A("a35","carry",161,166,c6,null,15,19), A("a36","pass",167,170,c6,a2,29,26,false),
                A("a37","carry",171,176,a2,null,45,26), A("a38","pass",177,180,a2,a3,58,32,true)),
            Phase(6,"turnover",Shape(29,21,48,32,42,53,62,12,73,43),Shape(62,23,45,49,38,28,23,42,38,12),
                A("a39","carry",181,186,a3,null,73,30), A("a40","pass",187,190,a3,a6,84,40,true),
                A("a41","carry",191,196,a6,null,95,34), A("a42","pass",197,200,a6,c2,73,24,false),
                A("a43","carry",201,206,c2,null,58,23), A("a44","pass",207,210,c2,c3,46,45,true)),
            Phase(7,"finish",Shape(40,17,62,31,51,53,76,12,85,42),Shape(70,24,49,45,43,28,27,40,48,10),
                A("a45","carry",211,216,c3,null,32,44), A("a46","pass",217,220,c3,c4,25,26,true),
                A("a47","carry",221,226,c4,null,13,27), A("a48","shot",227,230,c4,null,0,34),
                A("a49","kickoff",231,234,a4,null,52.5,34), A("a50","pass",235,238,a4,a5,63,13,true)),
            Phase(8,"response",Shape(25,18,46,32,37,50,63,13,70,43),Shape(59,23,44,49,35,29,20,41,34,11),
                A("a51","carry",241,247,a5,null,80,17), A("a52","pass",248,251,a5,a3,76,32,true),
                A("a53","carry",252,257,a3,null,88,32), A("a54","pass",258,261,a3,c2,76,24,false),
                A("a55","carry",262,267,c2,null,62,23), A("a56","pass",268,270,c2,c4,48,28,true)),
            Phase(9,"finish",Shape(40,18,62,32,51,53,80,12,87,43),Shape(74,23,52,49,46,28,30,40,55,11),
                A("a57","carry",271,276,c4,null,35,27), A("a58","pass",277,280,c4,a4,48,46,false),
                A("a59","carry",281,285,a4,null,65,45), A("a60","pass",286,288,a4,a6,90,35,true),
                A("a61","shot",289,291,a6,null,105,34), A("a62","kickoff",292,295,c3,null,52.5,34),
                A("a63","pass",296,298,c3,c6,49,15,true), A("a64","carry",298,300,c6,null,43,16))
        ]);
    }
    public static Match Create() => FiveMinutePlan.Build(Plan(), requireCommentary: false);
}
