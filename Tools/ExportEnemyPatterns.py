import argparse, json, sys
from pathlib import Path
parser=argparse.ArgumentParser()
parser.add_argument('--source-dir', required=True, type=Path)
parser.add_argument('--output-dir', required=True, type=Path)
args=parser.parse_args()
sys.path.insert(0,str(args.source_dir.resolve()))
from src.application import enemy_roster as regular, enemy_ai as bosses, elites
from src.domain.entities import Player, add_status, StatusEffect
stage=args.output_dir; q=lambda s:json.dumps(s,ensure_ascii=False)
status={'Veneno':'Poison','Marcado':'Marked','Débil':'Weak','Vulnerable':'Vulnerable','Frágil':'Frail','Enredado':'Entangled','Fuerza':'Strength','Espadas':'Blades','Mecha':'Fuse','Fuego':'Fire','Agua':'Water','Tierra':'Earth','Aire':'Air','Congelado':'Frozen'}
assets=dict(zip(list(regular.ENEMIES)+list(bosses.BOSSES)+list(elites.ELITES), ['wraith','slime','worm','eye','skull','bat','bomb','golem','acolyte','imp','mimic','mycelid','weaver','knight','executioner','hag','gargoyle','minotaur','scorpion']))
cycles={'espectro':3,'babosa':3,'gusano':3,'ojo':3,'craneo':3,'murcielago':3,'seta':3,'golem':3,'acolito':4,'diablillo':4,'mimico':4,'micelida':3,'tejedora':4,'caballero':6,'verdugo':4,'bruja':4,'gargola':3,'minotauro':3,'escorpion':4}
all_defs={**regular.ENEMIES,**bosses.BOSSES,**elites.ELITES}
def create(ai,f):
 if ai in regular.ENEMIES:return regular.create_enemy(ai,f,'e')
 if ai in bosses.BOSSES:return bosses.create_boss(ai,f,'e')
 return elites.create_elite(ai,f,'e')
def player():
 p=Player('Hero',100,100)
 p.status_effects=[StatusEffect(s,1,False) for s in ['Débil','Frágil','Vulnerable','Enredado']]
 return p
def sample(ai,f,step=0,mode='normal'):
 e=create(ai,f);e.ai_step=step
 if ai=='seta':
  for se in e.status_effects:
   if se.name=='Mecha':se.stacks=3-step
 if mode=='enrage': e.current_hp=e.max_hp//2
 if mode=='mend':e.current_hp=e.max_hp-1
 ally=regular.create_enemy('espectro',f,'ally')
 return bosses.next_intent(e,player(),[e,ally])
def emit_plan(ai,step=0,mode='normal'):
 samples={f:sample(ai,f,step,mode) for f in [1,2,4,8]};base=samples[1]
 def scalar(get):
  value=get(base)
  if all(get(x)==value for x in samples.values()):return str(value)
  if all(get(x)==int(value*(1+.15*(f-1))) for f,x in samples.items()):return f'EnemyCatalog.Scale({value}, floor)'
  raise ValueError((ai,step,mode,value,[get(x) for x in samples.values()]))
 def statuses(field):
  if not getattr(base,field):return 'null'
  return 'new[] { '+', '.join('new StatusAmount(StatusKind.'+status[name]+', '+scalar(lambda x,i=i:getattr(x,field)[i][1])+')' for i,(name,n) in enumerate(getattr(base,field)))+' }'
 cards='null' if not base.cards else 'new[] { '+', '.join('new StatusCardSpawn('+q(id)+', '+str(count)+', '+q(pile)+')' for id,count,pile in base.cards)+' }'
 return f'new EnemyIntent(IntentKind.{base.intent_type.name.title()}, {scalar(lambda x:x.value)}, {base.hits}, {q(base.move)}, {q(base.move_id)}, {q(base.description)}, block: {scalar(lambda x:x.block)}, buffs: {statuses("buffs")}, debuffs: {statuses("debuffs")}, allyBuffs: {statuses("ally_buffs")}, cards: {cards}, lifesteal: {str(base.lifesteal).lower()}, selfDestruct: {str(base.self_destruct).lower()}, allyBlock: {scalar(lambda x:x.ally_block)}, healAllies: {scalar(lambda x:x.heal_allies)})'
plans=['// Generated from the Python pattern functions; see Tools/ExportEnemyPatterns.py.','namespace LastWish.Domain','{','    public static partial class EnemyPatterns','    {','        private static EnemyIntent Normal(string ai, int step, int floor)','        {','            switch (ai)','            {']
for ai,n in cycles.items():
 plans+=['                case '+q(ai)+':','                    switch (step % '+str(n)+')','                    {']
 for i in range(n):plans+=['                        case '+str(i)+': return '+emit_plan(ai,i)+';']
 plans+=['                    }','                    break;']
plans+=['            }','            return new EnemyIntent(IntentKind.Attack, EnemyCatalog.Scale(18, floor), name: "Golpe de la Cripta", id: "crypt");','        }','        private static EnemyIntent Enrage(string ai, int floor)','        {','            switch (ai)','            {']
for ai in list(bosses.BOSSES)+list(elites.ELITES):plans+=['                case '+q(ai)+': return '+emit_plan(ai,0,'enrage')+';']
plans+=['            }','            return Normal(ai, 0, floor);','        }','        private static EnemyIntent Mend(int floor) => '+emit_plan('acolito',2,'mend')+';','        private static EnemyIntent Vengeance(int floor) => new EnemyIntent(IntentKind.Buff, name: "Venganza", id: "vengeance", block: EnemyCatalog.Scale(6, floor), buffs: new[] { new StatusAmount(StatusKind.Strength, 3) });','    }','}']
(stage/'Assets/Scripts/Domain/EnemyPatterns.Generated.cs').write_text('\n'.join(plans)+'\n',encoding='utf-8')
rows=[f'            new EnemyDefinition({q(ai)}, {q(d.name)}, {q(assets[ai])}, {d.hp}, {str(ai in bosses.BOSSES).lower()}, {str(ai in elites.ELITES).lower()}),' for ai,d in all_defs.items()]
(stage/'Assets/Scripts/Domain/EnemyDefinitions.Generated.cs').write_text('using System;\nusing System.Collections.Generic;\nnamespace LastWish.Domain\n{\n    public static partial class EnemyCatalog\n    {\n        public static readonly IReadOnlyList<EnemyDefinition> Definitions = Array.AsReadOnly(new[] {\n'+'\n'.join(rows)+'\n        });\n    }\n}\n',encoding='utf-8')
def signature(x):
 statuses=lambda arr:','.join(status[name]+':'+str(n) for name,n in arr)
 cards=','.join(id+':'+str(n)+':'+pile for id,n,pile in x.cards)
 return '|'.join([x.move_id,str(x.value),str(x.hits),str(x.block),statuses(x.buffs),statuses(x.debuffs),statuses(x.ally_buffs),str(x.ally_block),str(x.heal_allies),str(int(x.lifesteal)),str(int(x.self_destruct)),cards])
fixtures=[]
for ai in all_defs:
 for f in [1,2,7]:
  e=create(ai,f);p=player();trace=[]
  for t in range(24):
   trace.append(signature(e.intent))
   for name,n in e.intent.buffs:add_status(e.status_effects,name,n,is_buff=True)
   if t==3:e.current_hp=e.max_hp//2
   regular.after_action(e)
   e.intent=bosses.next_intent(e,p,[e])
  fixtures.append('            Check('+q(ai)+', '+str(f)+', new[] { '+', '.join(q(x) for x in trace)+' });')
test='''using System.Linq;
using LastWish.Domain;
using NUnit.Framework;
namespace LastWish.Tests
{
    public sealed class EnemyParityTests
    {
        [Test] public void AllNineteenPatternsMatchPythonAcrossFloorsAndEnrage()
        {
FIXTURES
        }
        private static void Check(string ai, int floor, string[] expected)
        {
            var enemy = EnemyCatalog.Create(ai, floor, "e"); var hero = new StatusSet();
            foreach (var kind in new[] { StatusKind.Weak, StatusKind.Frail, StatusKind.Vulnerable, StatusKind.Entangled }) hero.Add(kind, 1);
            for (var i = 0; i < expected.Length; i++)
            {
                var x = enemy.Intent;
                var signature = string.Join("|", x.Id, x.Value, x.Hits, x.Block, Statuses(x.Buffs), Statuses(x.Debuffs), Statuses(x.AllyBuffs), x.AllyBlock, x.HealAllies,
                    x.Lifesteal ? 1 : 0, x.SelfDestruct ? 1 : 0, string.Join(",", x.Cards.Select(c => c.Id + ":" + c.Count + ":" + c.Pile)));
                Assert.That(signature, Is.EqualTo(expected[i]), ai + " floor " + floor + " step " + i);
                foreach (var buff in x.Buffs) enemy.Statuses.Add(buff.Kind, buff.Amount);
                if (i == 3) enemy.LoseHp(enemy.Hp - enemy.MaxHp / 2);
                if (ai == "seta" && enemy.Statuses[StatusKind.Fuse] > 1) enemy.Statuses.Tick(StatusKind.Fuse);
                enemy.Plan(hero, new[] { enemy });
            }
        }
        private static string Statuses(StatusAmount[] statuses) => string.Join(",", statuses.Select(s => s.Kind + ":" + s.Amount));
    }
}
'''.replace('FIXTURES','\n'.join(fixtures))
(stage/'Assets/Tests/EditMode/EnemyParityTests.cs').write_text(test,encoding='utf-8')
print('Generated 19 patterns, 57 parity traces / 1368 expected intents')
