using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

public class AddNauseamScript : MonoBehaviour {

    public KMAudio Audio;
    public KMBombModule module;
    public KMBombInfo info;
    public List<KMSelectable> buttons;
    public TextMesh[] displays;

    private readonly string[] symbs = new string[22]
    {
        "----------$%----------",
        "---------&£&$---------",
        "--------£#%#£&--------",
        "-------%$&£&%#$-------",
        "------&£#%$#£&£%------",
        "-----#%&$&£%$%#$&-----",
        "----&$£#£#$&£&£%#$----",
        "---$#%&%$&£#%$#&£%£---",
        "--%£&$#£#%&$&£%$#&$%--",
        "-£&$#%&$&£#%#$&£%£#&$-",
        "%#%£&£#%#$&£&%#$&$%£#%",
        "&$&#%$&£&%#$#£&%£#&$&£",
        "-£%$&#%$#£&%&$#$&%£#%-",
        "--#£%$&%&$#£#%&£#$&$--",
        "---$#£#$£%&$&£#%&%£---",
        "----%&%&#$#%#%&$£#----",
        "-----#$£%£&$£$£#%-----",
        "------&#$#%#&%&$------",
        "-------%&£$£$#£-------",
        "--------$#%&%£--------",
        "---------&$#$---------",
        "----------£&----------"
    };
    private int num;
    private int stage;
    private int correct;
    private int prev;
    private int hl;
    private int[] ans = new int[4];
    private string[][] subsymb = new string[3][] { new string[4], new string[4], new string[4] { "-", "-", "-", "-"} };
    private int[] offset = new int[2];
    private bool startup;
    private bool tp;
    private string tpstring;
    private int nausea;

    private static int moduleIDCounter;
    private int moduleID;
    private bool moduleSolved;

    private void Awake()
    {
        moduleID = ++moduleIDCounter;
        tp = TwitchPlaysActive;
        foreach(KMSelectable button in buttons)
        {
            int b = buttons.IndexOf(button);
            button.OnHighlight = delegate () { if(!startup && stage > 0 && stage <= 20) displays[b + 3].text = new string[] { "\u2191", "\u2190", "\u2192", "\u2193" }[b]; hl = b + 1; };
            button.OnHighlightEnded = delegate () { if (!startup && stage > 0 && stage <= 20) displays[b + 3].text = ans[b].ToString(); hl = 0; };
            button.OnInteract = delegate ()
            {
                if (!moduleSolved && !startup)
                {
                    button.AddInteractionPunch(0.5f);
                    if (stage == 0)
                        StartCoroutine("StartUp");
                    else if (stage > 20)
                    {
                        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, button.transform);
                        for (int i = 0; i < 3; i++)
                            subsymb[2][i] = subsymb[2][i + 1];
                        subsymb[2][3] = subsymb[0][b];
                        displays[1].text = string.Join("", subsymb[2]);
                    }
                    else if (b == correct)
                    {
                        stage++;
                        Audio.PlaySoundAtTransform("Right", button.transform);
                        Debug.LogFormat("[Add Nauseam #{0}] Pressed {1}.", moduleID, "ULRD"[b]);
                        num += ans[correct];
                        prev = ans[correct];
                        switch (b)
                        {
                            case 0: offset[0]--; break;
                            case 1: offset[1]--; break;
                            case 2: offset[1]++; break;
                            case 3: offset[0]++; break;
                        }
                        if (stage <= 20)
                            Prompt();
                        else
                        {
                            displays[2].text = "";
                            int[] tl = new int[2] { (offset[0] / 2) + 10, (offset[1] / 2) + 10 };
                            string[] grid = Enumerable.Range(0, 4).Select(x => symbs[tl[0] + (x / 2)][tl[1] + (x % 2)].ToString()).ToArray();
                            Debug.Log(grid.Join());
                            displays[1].text = "----";
                            List<char> sl = info.GetSerialNumberLetters().Distinct().ToList();
                            Debug.Log(sl.Join());
                            Debug.Log(sl.OrderBy(x => x - 'A').Join());
                            List<int> order = sl.OrderBy(z => z - 'A').Select(x => sl.IndexOf(x)).ToList();
                            for (int i = order.Count(); i < 4; i++)
                                order.Add(i);
                            Debug.Log(order.Join());
                            string[] assign = order.Select(x => grid[x]).ToArray();
                            Debug.Log(assign.Join());
                            int[] digs = new int[4] { num / 64, (num / 16) % 4, (num / 4) % 4, num % 4 };
                            subsymb[1] = digs.Select(x => assign[x]).ToArray();
                            subsymb[0] = grid.Shuffle();
                            for (int i = 0; i < 4; i++)
                                displays[i + 3].text = subsymb[0][i].ToString();
                            Debug.LogFormat("[Add Nauseam #{0}] The sum of the answers is {1}.", moduleID, num);
                            Debug.LogFormat("[Add Nauseam #{0}] {1} in base 4 is {2}.", moduleID, num, string.Join("", digs.Select(x => x.ToString()).ToArray()));
                            Debug.LogFormat("[Add Nauseam #{0}] The grid is located {1} spaces {2} and {3} spaces {4} from the centre.", moduleID, Mathf.Abs(offset[0] / 2), offset[0] < 0 ? "up" : "down", Mathf.Abs(offset[1] / 2), offset[1] < 0 ? "left" : "right");
                            Debug.LogFormat("[Add Nauseam #{0}] The symbols have the values: {1}.", moduleID, string.Join(", ", Enumerable.Range(0, 4).Select(x => assign[x] + "=" + x.ToString()).ToArray()));
                            Debug.LogFormat("[Add Nauseam #{0}] The passcode is {1}.", moduleID, string.Join("", subsymb[1]));

                        }                           
                    }
                    else
                        Strike();
                }
                return false;
            };
        }
    }

    private IEnumerator StartUp()
    {
        startup = true;
        stage = 1;
        float time = 0;
        Audio.PlaySoundAtTransform("Startup", transform);
        Debug.LogFormat("[Add Nauseam #{0}] Starting Up...", moduleID);
        while(time < 1f)
        {
            time += Time.deltaTime;
            int t = (int)Mathf.Lerp(0, 150, time);
            displays[0].text = (t < 10 ? "0" : "") + t.ToString();
            yield return null;
        }
        Prompt();
        startup = false;
        for(int i = 150; i > 0; i--)
        {
            displays[0].text = (i < 10 ? "0" : "") + i.ToString();
            yield return new WaitForSeconds(1);
        }
        displays[0].text = "00";
        if(stage < 20)
        {
            Debug.LogFormat("[Add Nauseam #{0}] Out of Time.", moduleID);
            Strike();
        }
        else if (subsymb[1].SequenceEqual(subsymb[2]))
        {
            Debug.LogFormat("[Add Nauseam #{0}] Submitted {1}.", moduleID, string.Join("", subsymb[2]));
            module.HandlePass();
            moduleSolved = true;
            displays[1].text = "\u2713";
            displays[2].text = nausea > 2 ? "D-" : new string[] { "A+" , "B", "C"}[nausea];
            for (int i = 3; i < 7; i++)
                displays[i].text = "";
            if (nausea < 1 && Random.Range(0, 5) == 0)
                Audio.PlaySoundAtTransform("Best", transform);
            else
                Audio.PlaySoundAtTransform("Good", transform);
        }
        else
        {
            Debug.LogFormat("[Add Nauseam #{0}] Submitted {1}.", moduleID, string.Join("", subsymb[2]));
            Strike();
        }
    }

    private void Prompt()
    {
        correct = Random.Range(0, 4);
        int q = Random.Range(stage == 1 || stage % 10 == 0 ? 1 : 0, stage > 1 ? 6 : 5);
        List<int> a = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 }.Shuffle();
        switch (q)
        {
            case 0:
                a.Remove(stage % 10);
                displays[2].text = "Q#";
                for (int i = 0; i < 4; i++)
                    if (i == correct)
                        ans[i] = stage % 10;
                    else
                        ans[i] = a[i];
                break;
            case 1:
                int b = Random.Range(0, 2);
                a = a.Where(x => x % 2 == 1 - b).ToList();
                displays[2].text = b == 0 ? "EVEN" : "ODD";
                for (int i = 0; i < 4; i++)
                    if (i == correct)
                        ans[i] = b == 0 ? Random.Range(1, 5) * 2 : (Random.Range(1, 5) * 2) - 1;
                    else
                        ans[i] = a[i];
                break;
            case 5:
                ans = a.Take(4).ToArray();
                int shift = ans[correct] - prev;
                displays[2].text = "X";
                if (shift != 0)
                    displays[2].text += (shift > 0 ? "+" : "") + shift.ToString();
                break;
            default:
                ans = a.Take(4).ToArray();
                int op = Random.Range(ans[correct] > 3 ? 0 : 1, 3);
                int c = 0;
                switch (op)
                {
                    case 0:
                        c = Random.Range(1, 4);
                        displays[2].text = string.Format("{0}+{1}", ans[correct] - c, c);
                        break;
                    case 1:
                        c = Random.Range(1, 9);
                        displays[2].text = string.Format("{0}-{1}", ans[correct] + c, c);
                        break;
                    default:
                        c = Random.Range(2, 9);
                        displays[2].text = string.Format("{0}/{1}", ans[correct] * c, c);
                        break;
                }
                break;
        }
        displays[1].text = (q == 0 ? "??" : ((stage > 9 ? "" : "0") + stage.ToString())) + "/20";
        for (int i = 0; i < 4; i++)
            if(i != hl - 1)
                displays[i + 3].text = ans[i].ToString();
        if(tp)
            tpstring = string.Format("The prompt is \"{0}\". {1}.", displays[2].text, string.Join(", ", Enumerable.Range(0, 4).Select(x => "ULRD"[x] + "=" + ans[x]).ToArray()));
        Debug.LogFormat("[Add Nauseam #{0}] Question {1}: The prompt is \"{2}\" and the possible answers are {3}.", moduleID, stage, displays[2].text, string.Join(", ", Enumerable.Range(0, 4).Select(x => "ULRD"[x] + "=" + ans[x]).ToArray()));
    }

    private void Strike()
    {
        module.HandleStrike();
        StopCoroutine("StartUp");
        displays[0].text = "00";
        for (int i = 1; i < 7; i++)
            displays[i].text = "";
        stage = 0;
        num = 0;
        offset = new int[2];
        subsymb[2] = new string[4] { "-", "-", "-", "-" };
        nausea++;
        if (nausea == 3)
            Audio.PlaySoundAtTransform("Vom", transform);
    }

    bool TwitchPlaysActive;

#pragma warning disable 414
    private readonly string TwitchHelpMessage = "!{0} U/L/R/D [Presses directional button. Presses can be chained when entering the passcode.]";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToUpperInvariant().Replace(" ", "");
        if(command.Any(x => !"ULRD".Contains(x.ToString())))
        {
            yield return "sendtochaterror!f Only U, L, R, and D or valid commands.";
            yield break;
        }
        if(command.Length > 1 && stage <= 20)
        {
            yield return "sendtochaterror!f Only one answer to a prompt may be sent at a time.";
            yield break;
        }
        if(stage > 20)
        {
            int[] p = command.Select(x => "ULRD".IndexOf(x.ToString())).ToArray();
            for(int i = 0; i < p.Length; i++)
            {
                yield return null;
                buttons[p[i]].OnInteract();
            }
        }
        else
        {
            yield return null;
            buttons["ULRD".IndexOf(command)].OnInteract();
            while (startup)
                yield return null;
            yield return null;
            if(stage > 0 && stage <= 20)
                yield return "sendtochat!f " + tpstring;
        }
    }
}
