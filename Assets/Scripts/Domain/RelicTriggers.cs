using System;
using System.Linq;
using System.Collections.Generic;
namespace LastWish.Domain
{
    public sealed partial class CombatState
    {
        private CardDefinition resolvingCard;
        private uint resolvingMask;
        private int attacksPlayed, skillsPlayed, combosPlayed, spoilsPlayed, voidsTurn, voidsCombat, enduresCombat, furies;
        private int storedMana, turnAttackBonus, bonusAttack, stairCost = -1, stairLength, thiefGold, clockUses, daggersDrawn;
        private bool attackPlayedCombat, skillPlayedCombat, furyAttackDone, stairDone, manaVialUsed, watchUsed, healDrawUsed;
        private bool ringUsed, mistUsed, firstHitTaken, hiddenFirstAttack, debuffIgnored, reshuffled, lensUsed, hpLossDrawUsed, lastBreathUsed, hitLastEnemyTurn;
        private bool luckyCoinUsed;
        private EnemyState lastRandomTarget;
        private int elementMask;
        private HashSet<EnemyState> markedSeen = new HashSet<EnemyState>();
        private HashSet<EnemyState> checkedDeaths = new HashSet<EnemyState>();
        private static bool HasKeyword(CardDefinition card, EffectCondition condition) =>
            card.Effects != null && card.Effects.Any(e => e.Condition == condition)
            || condition == EffectCondition.Combo && card.ComboBlock > 0
            || condition == EffectCondition.Endure && card.EndureStrength > 0;
        private bool KeywordResolved(EffectCondition condition) => resolvingCard != null && HasKeyword(resolvingCard, condition) && (resolvingMask & 1u << (int)condition) != 0;
        private int MainRelicDamage(int target)
        {
            long bonus = turnAttackBonus + (long)RelicPower("fire_orb", 2) + bonusAttack;
            var card = resolvingCard;
            if (card == null) return Saturate(bonus);
            if (card.IsAttack)
            {
                if (!attackPlayedCombat) bonus += RelicPower("whetstone", 6);
                if (Turn == 3) bonus += RelicPower("war_horn", 4);
                if (Turn % 2 == 0) bonus += RelicPower("pendulum", 2);
                if (Hp * 2L < Hero.MaxHp) bonus += RelicPower("berserker_helm", 3);
                if (attacksPlayed == 0) bonus += Math.Min(Block, RelicPower("molten_iron", 15));
                if (KeywordResolved(EffectCondition.Fury) && !furyAttackDone) bonus += RelicPower("notched_axe", 4);
            }
            if (target >= 0 && enemies[target].Block > 0) bonus += RelicPower("breach_spear", 3);
            if (card.Upgraded) bonus += RelicPower("touchstone", 2);
            if (KeywordResolved(EffectCondition.Combo))
            { bonus += RelicPower("duelist_scarf", 2); if (combosPlayed == 2) bonus += RelicPower("harlequin_mask", 5); }
            return Saturate(bonus);
        }
        private int MainRelicBlock()
        {
            long bonus = 0;
            if (Turn == 1) bonus += RelicPower("oil_lamp", 3);
            if (Turn % 2 == 1) bonus += RelicPower("pendulum", 2);
            bonus += (long)Math.Max(0, Hero.MaxHp - Hp) / 10 * RelicPower("dented_plate");
            if (resolvingCard != null)
            {
                if (HasKeyword(resolvingCard, EffectCondition.Endure)) bonus += RelicPower("wristguard", 2);
                if (resolvingCard.Upgraded) bonus += RelicPower("touchstone", 2);
                if (KeywordResolved(EffectCondition.Combo)) bonus += RelicPower("duelist_scarf", 2);
            }
            return Saturate(bonus);
        }
        private void CombatStartRelics()
        {
            StartingRelics();
            var alive = enemies.Where(e => e.Alive).ToArray();
            if (alive.Length > 0)
            {
                alive.OrderByDescending(e=>e.Hp).First().Statuses.Add(StatusKind.Weak, RelicPower("votive_candle"));
                foreach (var enemy in alive) enemy.Statuses.Add(StatusKind.Vulnerable, RelicPower("shackle"));
                var normals=alive.Where(e=>!e.Definition.Boss&&!e.Definition.Elite).ToArray();
                if (normals.Length > 0 && HasRelic("frost_amulet")) normals[random.Next(normals.Length)].Statuses.Add(StatusKind.Frozen,1);
                if (HasRelic("willow_wand")) ApplyElement(enemies.IndexOf(alive[random.Next(alive.Length)]),ElementKind.Water,RelicPower("willow_wand",2));
                alive.OrderBy(e=>e.Hp).First().Statuses.Add(StatusKind.Marked,RelicPower("folding_knife",2));
            }
            if (Hp * 2L < Hero.MaxHp) HealHero(RelicPower("phoenix_feather",8));
            if (Hp >= Hero.MaxHp && HasRelic("blood_skin")) { Hp -= Math.Min(5,Hp-1); GainStrength(RelicPower("blood_skin",2)); }
            if (alive.Any(e=>e.Definition.Boss)) for (var i=0;i<RelicPower("racing_heart");i++) GainStrength(Strength);
            var curse=RelicPower("cursed_contract");
            if (curse > 0) { manaMaximum=Saturate((long)manaMaximum+curse); Mana=ManaMaximum; SpawnStatusCards(new StatusCardSpawn("status_herida",curse,"draw")); }
            HideDaggers(RelicPower("dagger_pouch",2));
            var innate=draw.Where(c=>c.Innate).ToArray();draw.RemoveAll(c=>c.Innate);draw.AddRange(innate);
        }
        private void DaggerHit()
        {
            var alive=enemies.Where(e=>e.Alive).ToArray();
            if(alive.Length>0)
            {
                var enemy=alive[random.Next(alive.Length)];
                var damage=4L+RelicPower("sharp_sheath",2)+(daggersDrawn==0?RelicPower("pocket_sharpener",4):0);
                damage+=(long)(enemy.Statuses[StatusKind.Poison]/3)*RelicPower("fox_tail");
                enemy.Damage(damage);
                var twins=RelicPower("twin_daggers");var others=enemies.Where(e=>e.Alive&&e!=enemy).ToArray();
                if(twins>0&&others.Length>0)others[random.Next(others.Length)].Damage(damage/2*twins);
            }
            daggersDrawn++;HideDaggers(daggersOnDraw);
        }
        private void OpeningHandRelics()
        {
            if (HasRelic("d20"))
            { var options=Enumerable.Range(0,hand.Count).Where(i=>hand[i].Chroma==ChromaKind.None&&!hand[i].Id.StartsWith("status_")).ToArray();if(options.Length>0) { var index=options[random.Next(options.Length)];hand[index]=hand[index].WithChroma(ChromaKind.Golden); } }
            if(singularDeck) for(var i=0;i<RelicPower("singular_locket")&&hand.Count<10;i++) hand.Add(new CardDefinition("t_daga_oculta","Daga Oculta",0,0,0,kind:CardKind.Attack));
            var marble=RelicPower("marble_bag"); if (marble > 0 && DiscardRandom()!=null) AddBlock(3L*marble);
        }
        private void EndTurnRelics()
        {
            if (hand.Count == 0) AddBlock(RelicPower("spider_thread",6));
            if (cardsPlayed >= 3) AddBlock(RelicPower("sandbag",4));
            if (cardsPlayed == 2) AddBlock(RelicPower("chess_piece",5));
            if (attacksPlayed > 0 && attacksPlayed == skillsPlayed) AddBlock(RelicPower("balance_bracelet",5));
            AddBlock((long)Mana*RelicPower("empty_bowl",3));
            if (Mana > 0) storedMana=Math.Min(Mana,RelicPower("arcane_battery"));
            if (HpLostThisTurn > 0) HealHero(RelicPower("field_bandage",2));
            if (Block == 0) AddBlock(RelicPower("ragged_cape",3));
        }
        private void ResetRelicTurn()
        {
            attacksPlayed=skillsPlayed=combosPlayed=spoilsPlayed=voidsTurn=turnAttackBonus=stairLength=elementMask=0;
            stairCost=-1; furyAttackDone=stairDone=manaVialUsed=watchUsed=healDrawUsed=ringUsed=mistUsed=hpLossDrawUsed=luckyCoinUsed=false;
            lastRandomTarget=null;
        }
        private int StartTurnRelics()
        {
            Mana=Saturate((long)Mana+storedMana); storedMana=0;
            if (Block == 0) turnAttackBonus=RelicPower("brazier",3);
            long incoming=0; foreach(var enemy in enemies) if(enemy.Alive && enemy.Intent.Kind==IntentKind.Attack) incoming+=(long)enemy.HitDamage(HeroStatuses)*Math.Max(1,enemy.Intent.Hits);
            if (Block > incoming) GainStrength(RelicPower("legion_banner"));
            return Turn % 4 == 0 ? RelicPower("hourglass") : 0;
        }
        private bool ClearMarked(EnemyState enemy)
        {
            if (enemy.Statuses[StatusKind.Marked] <= 0) { markedSeen.Remove(enemy); return false; }
            if (!HasRelic("black_silk")) return true;
            if (markedSeen.Remove(enemy)) return true;
            markedSeen.Add(enemy); return false;
        }
        private int ElementBonus(CardDefinition card, ElementKind element)
        {
            var tag=element==ElementKind.Fire?"tinder":element==ElementKind.Water?"seashell":element==ElementKind.Earth?"pumice":"griffin_feather";
            elementMask |= 1 << (int)element;
            return RelicPower(tag)+(card.Cost==0?RelicPower("pointy_hat"):0);
        }
        private void AfterPlayRelics(int target, int cost)
        {
            var card=resolvingCard;
            var combo=KeywordResolved(EffectCondition.Combo); var spoil=KeywordResolved(EffectCondition.Spoil);
            var fury=KeywordResolved(EffectCondition.Fury);var endure=KeywordResolved(EffectCondition.Endure);var empty=KeywordResolved(EffectCondition.Void);
            if (combo) { AddBlock(RelicPower("evasion_brooch")); HitRandom(RelicPower("throwing_knife")); }
            if (spoil) { AddBlock(RelicPower("rag_sack",2)); if (spoilsPlayed==0) DrawCards(RelicPower("grappling_hook")); }
            if (card.IsAttack && attacksPlayed==0) HealHero(RelicPower("iron_fang",2));
            if (fury && ++furies%3==0) GainStrength(RelicPower("torn_banner"));
            CheckRelicDeaths(); SpreadPoison();
            if (card.IsAttack) { attackPlayedCombat=true; if(fury) furyAttackDone=true; }
            if (card.Kind==CardKind.Skill) { skillPlayedCombat=true; skillsPlayed++; }
            if (combo) { if(combosPlayed==0) PoisonEnemy(enemies[target],RelicPower("poisoned_scarf",2)); combosPlayed++; }
            if (endure && ++enduresCombat%3==0) GainStrength(RelicPower("war_drum"));
            if (empty)
            {
                if(voidsTurn==0) AddBlock(RelicPower("water_clock",4)); voidsTurn++;voidsCombat++;
                if(voidsCombat%3==0) pendingDraws+=RelicPower("blank_grimoire");
                if(card.Cost>=3) GainMana(RelicPower("lesser_fount"));
            }
            if (spoil && !mistUsed && HasRelic("mist_flask"))
            { var alive=enemies.Where(e=>e.Alive).OrderByDescending(e=>e.Hp).FirstOrDefault(); if(alive!=null) { mistUsed=true;alive.Statuses.Add(StatusKind.Weak,RelicPower("mist_flask")); } }
            stairLength=stairLength>0 && cost==stairCost+1?stairLength+1:1;stairCost=cost;
            if(stairLength>=3&&!stairDone&&HasRelic("stairway")) { stairDone=true;var n=RelicPower("stairway");bonusAttack=Saturate((long)bonusAttack+n);bonusDexterity=Saturate((long)bonusDexterity+n); }
            var played=cardsPlayed+1;
            if(played>=4&&Mana==0&&!manaVialUsed&&HasRelic("mana_vial")) { manaVialUsed=true;GainMana(RelicPower("mana_vial")); }
            if(played>=6&&!watchUsed&&HasRelic("stolen_watch")) { watchUsed=true;nextDiscount=99; }
            if(elementMask==15&&!ringUsed&&HasRelic("ring_of_four")) { ringUsed=true;manaMaximum=Saturate((long)manaMaximum+RelicPower("ring_of_four")); }
            var draws=pendingDraws;pendingDraws=0;DrawCards(draws);
            if(card.IsAttack) attacksPlayed++;if(spoil)spoilsPlayed++;
        }
        private void CheckRelicDeaths()
        {
            foreach(var enemy in enemies) if(!enemy.Alive&&checkedDeaths.Add(enemy))
            { HealHero(RelicPower("dandelion",3));GainStrength(RelicPower("fang_necklace"));if(enemy.Statuses[StatusKind.Poison]>0)nextDiscount+=RelicPower("thief_king_crown"); }
        }
        private void PoisonEnemy(EnemyState enemy, int amount)
        { if(enemy==null||!enemy.Alive||amount<=0)return; if(enemy.Statuses[StatusKind.Marked]>0)amount=Saturate((long)amount*(1+RelicPower("scorpion_venom")));enemy.Statuses.Add(StatusKind.Poison,amount); }
        private void SpreadPoison()
        {
            var multiplier=RelicPower("viper_fang");if(multiplier<=0)return;
            foreach(var enemy in enemies) if(!enemy.Alive&&enemy.Statuses[StatusKind.Poison]>0)
            { var stacks=enemy.Statuses[StatusKind.Poison];enemy.Statuses.Remove(StatusKind.Poison);var alive=enemies.Where(e=>e.Alive).ToArray();if(alive.Length>0)alive[random.Next(alive.Length)].Statuses.Add(StatusKind.Poison,Saturate((long)stacks*multiplier)); }
        }
        private void AfterCompletedPlay()
        {
            CheckRelicDeaths();SettlePickpocket();
            if(clockUses<RelicPower("broken_clock")&&Mana==0&&hand.Count>0) { clockUses++;Mana=ManaMaximum; }
        }
        private int RelicIncomingDamage(int damage, EnemyState attacker)
        {
            if (damage>0)
            {
                if(!firstHitTaken) { firstHitTaken=true;damage=Math.Max(0,damage-RelicPower("white_flag",4)); }
                if(Turn==1&&!hiddenFirstAttack&&HasRelic("capuche")) { hiddenFirstAttack=true;damage/=2; }
            }
            attacker.Damage(RelicPower("thorn_crown",2));return damage;
        }
        private int OnReshuffle()
        { AddBlock(RelicPower("tidy_pile",4));if(reshuffled)return 0;reshuffled=true;return RelicPower("magic_deck"); }
    }
}
