using UnityEngine;

public readonly struct OptResult
{
    public readonly float damage;
    public readonly bool pierce;
    public readonly float exitVel;
    public readonly float remainingPen;

    public OptResult(float dmg, bool p, float v, float remPen)
    {
        damage = dmg;
        pierce = p;
        exitVel = v;
        remainingPen = remPen;
    }
}

public static class OptFormula
{
    public static OptResult Calculate(float atk, float pen, float def, float muzzleVel)
    {
        if (def <= 0f)
        {
            return new OptResult(atk * 2f, true, muzzleVel, pen); // If 0 DEF, max bonus (+100%) and pierce, zero pen lost
        }

        float ratio = pen / def;

        // 1. If PEN is under DEF, attack deals 0 damage and does not pierce.
        if (ratio < 1.0f)
        {
            return new OptResult(0f, false, 0f, 0f);
        }

        // 2 & 3. If PEN >= DEF, increase ATK based on percentage, up to a max of +100% (2x ATK).
        // For example: ratio 1.0 -> 0% bonus. ratio 1.5 -> 50% bonus. ratio 2.0+ -> 100% bonus.
        float bonusPercent = Mathf.Clamp(ratio - 1.0f, 0f, 1.0f);
        float effectiveAtk = atk * (1f + bonusPercent);

        // 4. If PEN is at least 200% of DEF (ratio >= 2.0), it pierces.
        if (ratio < 2.0f)
        {
            // Tidak tembus (No Pierce)
            return new OptResult(effectiveAtk, false, 0f, 0f);
        }

        // Tembus (Pierce == true)
        // Semakin besar DEF dibandingkan PEN, semakin besar resistance-nya (maksimal 0.5 atau 50% loss jika ratio = 2.0)
        float resistance = def / pen; 
        
        // Peluru kehilangan kecepatan berdasarkan resistance dari armor
        float exitVel = Mathf.Max(0f, muzzleVel * (1f - resistance));
        
        float velRatio = exitVel / muzzleVel;
        float finalDmg = effectiveAtk * velRatio;
        
        // PENETRATION DROP: Pen loses exactly the amount of DEF it punched through
        float remainingPen = Mathf.Max(0f, pen - def);

        return new OptResult(finalDmg, true, exitVel, remainingPen);
    }
}