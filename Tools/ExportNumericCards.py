import argparse, json, sys
parser = argparse.ArgumentParser(description="Export supported native card rules and replay fixtures from the Python source")
parser.add_argument("--source-dir", type=str, default=".")
parser.add_argument("--output-dir", default="tmp/unity-port-next")
args = parser.parse_args()
from pathlib import Path
sys.path.insert(0, str(Path(args.source_dir).resolve()))
from src.domain.card_pool import PackTheme, card_factories_for_theme
from src.domain.card import CardType
from src.domain.combat import CombatState
from src.domain.entities import Player, Enemy, add_status
from src.domain.pile import Hand, DrawPile, DiscardPile
from src.domain.mana import Mana
from src.application.play_card import play_card
stage=Path(args.output_dir);q=lambda s:json.dumps(s,ensure_ascii=False)
conditions=['combo','singular','void','spoil','reaction','fury','endure']
named = {
"_on_shield_bash":"ShieldBash", "_on_retaliation":"Retaliation", "_on_living_armor":"LivingArmor", "_on_double_block":"DoubleBlock", "_on_transfusion":"Transfusion", "_on_execution":"Execution", "_on_boiling_blood":"BoilingBlood", "_on_inner_fortress":"InnerFortress", "_on_second_skin":"SecondSkin", "_on_steel_skin":"SteelSkin", "_on_berserker":"Berserker", "_on_iron_will":"IronWill", "_on_keep_block":"RetainBlock", "_on_retain_block":"RetainBlock",
"_on_fan_flames":"FanFlames", "_on_combustion":"Combustion", "_on_combustion_all":"CombustionAll", "_on_fire_heart":"FireHeart", "_on_avalanche":"Avalanche", "_on_granite_skin":"GraniteSkin", "_on_hurricane_eye":"HurricaneEye", "_on_catalyst":"Catalyst", "_on_archmage":"Archmage", "_on_convergence":"Convergence", "_on_weaken_all_1":"WeakenAll", "_on_weaken_all":"WeakenAll",
"_on_golpe_total":"RawArea, 10", "_on_instinto":"HandDamage", "_on_muro_de_mana":"ManaBlock", "_on_retribucion":"DiscardBlock", "_on_veneno":"Poison", "_on_vision_del_caos":"DrawPileArea", "_on_grito_de_guerra":"ManaPerEnemy", "_on_lluvia_de_golpes":"DiscardAttackArea", "_on_mazo_impecable":"CheapHandHit", "_on_tormenta_veneno":"PoisonAll", "_on_refund_on_kill":"RefundKill", "_on_next_card_cheaper":"NextDiscount", "_on_first_card_cheaper":"FirstDiscount", "_on_next_damage_plus_6":"NextDamage", "_on_combo_always":"ComboAlways", "_on_gain_dexterity":"Dexterity", "_on_draw_one_of_each_type":"DrawTypes", "_on_turn_blades":"TurnBlades", "_on_break_guard":"BreakGuard", "_on_flurry":"Flurry", "_on_empty_pockets":"EmptyPockets", "_on_sleight_of_hand":"SleightOfHand", "_on_mark_target":"Mark"
}
named.update({
"_on_discard_random":"DiscardRandom", "_on_random_hit_4":"RandomHit, 4", "_on_random_hit_3":"RandomHit, 3", "_on_two_random_10":"TwoRandomHits", "_on_all_hit_5":"RawArea, 5", "_on_hide_one_dagger":"HideDaggers, 1", "_on_hide_two_daggers":"HideDaggers, 2", "_on_lucky_cut":"LuckyCut", "_on_decoy":"Decoy", "_on_counter":"Counter", "_on_pickpocket":"Pickpocket", "_on_poison_blade":"PoisonBlade", "_on_roll_dice":"RollDice", "_on_scavenge":"Scavenge", "_on_dagger_master":"DaggerMaster", "_on_twin_shadow":"TwinShadow", "_on_bold_fortune":"BoldFortune", "_on_nothing_to_lose":"NothingToLose", "_on_perfect_chain":"PerfectChain", "_on_assassin":"Assassin", "_on_thousand_cuts":"ThousandCuts"
})
closures={"_on_lose":"LoseHp", "_on_heal":"Heal", "_on_strength":"Strength", "_on_mana":"Mana", "_on_heal_last_hit":"Lifesteal", "_on_strike":"Strike", "_on_area":"Area", "_on_heal_per_area_hit":"HealArea", "_on_all_hit":"MageArea", "_on_hit_target":"RawHit"}
def commands(cb):
 if cb is None:return []
 if cb.__name__ in named:return ["new CardCommand(CommandKind."+named[cb.__name__]+")"]
 owner=cb.__qualname__.split('.')[0]
 cells=dict(zip(cb.__code__.co_freevars,[cell.cell_contents for cell in cb.__closure__ or []]))
 if owner=='_then':return [command for child in cells['effects'] for command in commands(child)]
 if owner in closures:
  amount=cells.get('amount',cells.get('divisor',0));times=cells.get('times',1)
  return [f"new CardCommand(CommandKind.{closures[owner]}, {amount}, {times})"]
 raise ValueError(cb.__qualname__)
def callback_supported(cb):
 try: commands(cb);return True
 except ValueError:return False
def simple(fx):
 if not callback_supported(fx.on_play) or not callback_supported(fx.on_turn_start):return False
 for key in conditions:
  child=getattr(fx,key)
  if child is not None and (not callback_supported(child.on_play) or not callback_supported(child.on_turn_start) or any(getattr(child,k) is not None for k in conditions)):return False
 return True
def supported(c):
 return (c.card_type in (CardType.ATTACK,CardType.SKILL,CardType.POWER) and all(simple(f) for f in c.all_effects()) and not c.modifiers and not c.upgrade and c.max_upgrades==1
   and not any([c.play_on_draw,c.innate,c.unplayable,c.exhaust,c.ethereal,c.on_draw,c.on_turn_end_in_hand]))
factories=[f for theme in PackTheme for f in card_factories_for_theme(theme)];supported_factories=[f for f in factories if supported(f())]
def layer(fx,condition):
 el='null' if fx.element is None else 'ElementKind.'+fx.element.name.title()
 return f'new EffectLayer(EffectCondition.{condition}, {fx.damage.resolve()}, {fx.block.resolve()}, {fx.draw}, {fx.mana_gain}, {el}, {fx.element_stacks}, {str(fx.element_all).lower()}, new CardCommand[] {{ '+', '.join(commands(fx.on_play))+' }, new CardCommand[] { '+', '.join(commands(fx.on_turn_start))+f' }}, {q(fx.text)})'
rows=[]
for f in supported_factories:
 c=f();fx=c.base_effect
 layers=[]
 for effect in c.all_effects():
  layers.append(layer(effect,'Always'))
  layers += [layer(getattr(effect,key),key.title()) for key in conditions if getattr(effect,key) is not None]
 rows.append(f'            new CardDefinition({q(c.id)}, {q(c.name)}, {c.cost}, {fx.damage.resolve()}, {fx.block.resolve()}, {fx.draw}, {fx.mana_gain}, false, {c.rarity.value}, {q(c.card_class.value)}, {q(f.theme.value)}, effects: new[] {{ '+', '.join(layers)+f' }}, kind: CardKind.{c.card_type.name.title()}),')
source='''using System;
using System.Collections.Generic;
namespace LastWish.Domain
{
    // Generated from supported original card factories, including keywords, elements and native commands.
    public static class NumericCards
    {
        public static readonly IReadOnlyList<CardDefinition> All = Array.AsReadOnly(new[] {
ROWS
        });
    }
}
'''.replace('ROWS','\n'.join(rows))
(stage/'Assets/Scripts/Domain/NumericCards.Generated.cs').write_text(source,encoding='utf-8')
status=['Fuego','Agua','Tierra','Aire','Débil','Vulnerable','Congelado']
fixtures=[]
for f in supported_factories:
 if f.card_id in {"r_corte_afortunado", "r_tirar_los_dados"}: continue
 for mode in [False,True]:
  c=f();p=Player('Hero',100,50 if mode else 100,dexterity=4,attack_bonus=4,luck=2)
  enemy=Enemy('e','Objetivo',10000,10000)
  state=CombatState(p,[enemy],Hand([c]),DrawPile(),DiscardPile(),Mana(c.cost if mode else 10,10),singular_deck=True)
  if mode:
   state.cards_played_this_turn=1;state.discards_this_turn=1;state.hp_lost_this_turn=2;p.block=5
   add_status(enemy.status_effects,'Fuego',2,is_buff=False)
  result=play_card(state,0,0)
  assert result.success,(c.id,result.message)
  vals=[enemy.current_hp,p.block,state.mana.current,state.hand.count,state.discard_pile.count]
  vals += [p.current_hp,state.hero_strength,len(state.active_powers),state.card_cost(__import__('src.domain.card',fromlist=['Card']).Card('probe','probe',CardType.ATTACK,1,__import__('src.domain.card',fromlist=['CardEffect']).CardEffect('probe')))]
  vals += [sum(s.stacks for s in enemy.status_effects if s.name==kind) for kind in status]
  fixtures.append('            Check('+q(c.id)+', '+str(mode).lower()+', new[] { '+', '.join(str(v) for v in vals)+' });')
test='''using System;
using System.Linq;
using System.Reflection;
using LastWish.Domain;
using NUnit.Framework;
namespace LastWish.Tests
{
    public sealed class NumericCardParityTests
    {
        [Test] public void CardResolutionMatchesPythonWithAndWithoutReadyKeywords()
        {
FIXTURES
        }
        private static void Check(string id, bool ready, int[] expected)
        {
            var card = NumericCards.All.Single(c => c.Id == id);
            var enemy = new EnemyState("e", new EnemyDefinition("fixture", "Objetivo", "wraith", 10000), 1, 10000);
            var hero = HeroDefinition.OriginalFor(HeroKind.Warrior).WithBonuses(0, 8, 0);
            var state = new CombatState(hero, new[] { card }, 42, ready ? 50 : 100, new[] { enemy });
            if (ready)
            {
                typeof(CombatState).GetField("cardsPlayed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(state, 1);
                typeof(CombatState).GetField("discardsThisTurn", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(state, 1);
                typeof(CombatState).GetProperty("HpLostThisTurn").SetValue(state, 2);
                typeof(CombatState).GetProperty("Block").SetValue(state, 5);
                typeof(CombatState).GetProperty("Mana").SetValue(state, card.Cost);
                enemy.Statuses.Add(StatusKind.Fire, 2);
            }
            Assert.That(state.PlayCard(0), Is.True, id);
            var actual = new[] { enemy.Hp, state.Block, state.Mana, state.Hand.Count, state.DiscardCount, state.Hp, state.Strength, state.ActivePowers.Count, state.CardCost(new CardDefinition("probe", "probe", 1, 0, 0)),
                enemy.Statuses[StatusKind.Fire], enemy.Statuses[StatusKind.Water], enemy.Statuses[StatusKind.Earth], enemy.Statuses[StatusKind.Air],
                enemy.Statuses[StatusKind.Weak], enemy.Statuses[StatusKind.Vulnerable], enemy.Statuses[StatusKind.Frozen] };
            Assert.That(actual, Is.EqualTo(expected), id + " keywords=" + ready);
        }
    }
}
'''.replace('FIXTURES','\n'.join(fixtures))
(stage/'Assets/Tests/EditMode/NumericCardParityTests.cs').write_text(test,encoding='utf-8')
# Multi-turn fixtures use identical filler cards so Python/C# shuffle differences do not affect them.
from src.domain.card import Card, CardEffect
from src.domain.entities import Intent, IntentType
from src.application.end_turn import end_player_turn
power_rows=[]
for factory in supported_factories:
 c=factory()
 if c.card_type != CardType.POWER or c.id=='r_fortuna_audaz':continue
 hero=Player('Hero',100,50,dexterity=4,attack_bonus=4,luck=2)
 target=Enemy('e','Objetivo',10000,10000)
 fillers=[Card('filler','Relleno',CardType.SKILL,0,CardEffect('Relleno')) for _ in range(100)]
 state=CombatState(hero,[target],Hand([c]),DrawPile(fillers),DiscardPile(),Mana(10,10),singular_deck=True)
 state.turn_start_alive=1
 assert play_card(state,0,0).success
 for step in range(4):
  if step:
   target.intent=Intent(IntentType.ATTACK,0)
   end_player_turn(state)
  values=[hero.current_hp,hero.block,state.hero_strength,state.mana.current,target.current_hp,
          sum(s.stacks for s in target.status_effects if s.name=='Fuego'),state.hand.count,state.draw_pile.count,state.discard_pile.count]
  power_rows.append('            Check('+q(c.id)+', '+str(step)+', new[] { '+', '.join(map(str,values))+' });')
power_test="""using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LastWish.Domain;
using NUnit.Framework;
namespace LastWish.Tests
{
    public sealed class PowerTurnParityTests
    {
        [Test] public void PersistentPowersMatchPythonAcrossThreeFollowingTurns()
        {
ROWS
        }
        private static void Check(string id, int turns, int[] expected)
        {
            var card = NumericCards.All.Single(c => c.Id == id);
            var enemy = new EnemyState("e", new EnemyDefinition("fixture", "Objetivo", "wraith", 10000), 1, 10000);
            var hero = HeroDefinition.OriginalFor(HeroKind.Warrior).WithBonuses(0, 8, 0);
            var state = new CombatState(hero, new[] { card }, 42, 50, new[] { enemy });
            var draw = (List<CardDefinition>)typeof(CombatState).GetField("draw", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state);
            for (var i = 0; i < 100; i++) draw.Add(new CardDefinition("filler", "Relleno", 0, 0, 0));
            Assert.That(state.PlayCard(0), Is.True, id);
            for (var i = 0; i < turns; i++)
            { typeof(EnemyState).GetProperty("Intent").SetValue(enemy, new EnemyIntent(IntentKind.Attack, 0)); state.EndTurn(); }
            var actual = new[] { state.Hp, state.Block, state.Strength, state.Mana, enemy.Hp,
                enemy.Statuses[StatusKind.Fire], state.Hand.Count, state.DrawCount, state.DiscardCount };
            Assert.That(actual, Is.EqualTo(expected), id + " turn=" + turns);
        }
    }
}
""".replace('ROWS','\n'.join(power_rows))
(stage/'Assets/Tests/EditMode/PowerTurnParityTests.cs').write_text(power_test,encoding='utf-8')
coverage={'replayExcludedRandomCards':['r_corte_afortunado','r_tirar_los_dados'], 'total':len(factories),'implementedCards':[f.card_id for f in supported_factories], 'pending':[f.card_id for f in factories if f not in supported_factories]}
(stage/'docs/migration').mkdir(parents=True,exist_ok=True);(stage/'docs/migration/card-coverage.json').write_text(json.dumps(coverage,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'{len(power_rows)} multi-turn power fixtures exported')
print(f'{len(supported_factories)} cards implemented with {len(fixtures)} Python replay fixtures; {len(factories)-len(supported_factories)} pending')
