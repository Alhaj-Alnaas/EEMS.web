using Services.AccessControl;
var body = "user uid=1\tcardno=\tpin=10067\tpassword=\tgroup=1\tstarttime=0\tendtime=0\tname=\tprivilege=0\tdisable=0\tverify=0\nuser uid=2\tcardno=\tpin=6300\tpassword=\tgroup=1\tstarttime=0\tendtime=0\tname=\tprivilege=0\tdisable=0\tverify=0\n";
var r = ZkTecoUserInfoParser.Parse(body, "user");
Console.WriteLine("count=" + r.Count);
foreach (var e in r) Console.WriteLine(e.Pin + " name=" + e.Name + " uid=" + e.Uid);
