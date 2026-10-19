using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using LastWish.Domain;
using NUnit.Framework;
namespace LastWish.Tests
{
    public sealed class RelicCardParityTests
    {
        [Test] public void AllDeterministicCardsMatchPythonWithEveryNormalAndGoldenRelic()
        {
            var cards=NumericCards.All.ToDictionary(c=>c.Id);var relics=RelicCatalog.All.ToDictionary(r=>r.Id);
            var failures=new List<string>();var rows=0;
            using(var file=File.OpenRead(FixturePath()))
            using(var compressed=new GZipStream(file,CompressionMode.Decompress))
            using(var reader=new StreamReader(compressed))
            {
                string line;while((line=reader.ReadLine())!=null)
                {
                    var parts=line.Split('|');var relicId=parts[0];var golden=parts[1]=="1";var id=parts[2];
                    var expected=parts.Skip(3).Select(int.Parse).ToArray();
                    var enemy=new EnemyState("e",new EnemyDefinition("fixture","Objetivo","wraith",10000),1,10000);
                    var relic=new OwnedRelic(relics[relicId],golden?ChromaKind.Golden:ChromaKind.None);
                    var state=new CombatState(HeroDefinition.OriginalFor(HeroKind.Warrior).WithBonuses(0,8,0),new[]{cards[id]},42,50,new[]{enemy},relics:new[]{relic});
                    SetField(state,"cardsPlayed",1);SetField(state,"discardsThisTurn",1);
                    typeof(CombatState).GetProperty("HpLostThisTurn").SetValue(state,2);typeof(CombatState).GetProperty("Block").SetValue(state,5);
                    if(!Enumerable.Range(0,4).Any(i=>enemy.Statuses[(StatusKind)((int)StatusKind.Fire+i)]>0))enemy.Statuses.Add(StatusKind.Fire,2);
                    var selected=-1;for(var i=0;i<state.Hand.Count;i++)if(state.Hand[i].Id==id){selected=i;break;}
                    var success=false;
                    if(selected>=0) { typeof(CombatState).GetProperty("Mana").SetValue(state,state.CardCost(state.Hand[selected])); success=state.PlayCard(selected); }
                    var actual=new List<int>{success?1:0,state.Hp,state.Block,state.Strength,state.Mana,state.ManaMaximum,enemy.Hp,enemy.Block,state.Hand.Count,state.DrawCount,state.DiscardCount,state.HpLostCombat,state.GoldEarned,state.ActivePowerCount,state.LastHit,state.Hero.Dexterity+Field(state,"bonusDexterity"),state.Hero.AttackBonus+Field(state,"bonusAttack")};
                    actual.AddRange(Enumerable.Range(0,14).Select(i=>enemy.Statuses[(StatusKind)i]));actual.Add(state.HeroStatuses[StatusKind.Poison]);
                    if(!actual.SequenceEqual(expected))
                    { if(failures.Count<10)failures.Add(relicId+"/"+id+" golden="+golden+" expected="+string.Join(",",expected)+" actual="+string.Join(",",actual)); }
                    rows++;
                }
            }
            Assert.That(rows,Is.EqualTo(50856));Assert.That(failures.Count,Is.Zero,string.Join("\n",failures));
        }
        private static void SetField(CombatState state,string field,int value)=>typeof(CombatState).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(state,value);
        private static int Field(CombatState state,string field)=>(int)typeof(CombatState).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(state);
        private static string FixturePath()
        {
            foreach(var start in new[]{Directory.GetCurrentDirectory(),AppContext.BaseDirectory})
                for(var folder=new DirectoryInfo(start);folder!=null;folder=folder.Parent)
                { var path=Path.Combine(folder.FullName,"Assets/Tests/EditMode/Fixtures/relic-card-parity.csv.gz.bytes");if(File.Exists(path))return path; }
            throw new FileNotFoundException("Missing generated Python relic/card oracle");
        }
    }
}
