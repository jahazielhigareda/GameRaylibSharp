1namespace Server.ECS.Components
{
    public class StatsComponent
    {
        public int Level { get; set; } // Nivel del jugador
        public int Experience { get; set; } // Experiencia acumulada
        public int MaxHP { get; set; } // Salud máxima
        public int CurrentHP { get; set; } // Salud actual
        public int MaxMP { get; set; } // Mana máximo
        public int CurrentMP { get; set; } // Mana actual

        // Constructor
        public StatsComponent(int level, int maxHP, int maxMP)
        {
            Level = level;
            Experience = 0; // La experiencia comienza en 0
            MaxHP = maxHP;
            CurrentHP = maxHP; // Salud total al inicio
            MaxMP = maxMP;
            CurrentMP = maxMP; // Mana total al inicio
        }

        /// <summary>
        /// Incrementa la experiencia y sube de nivel si la experiencia requerida es alcanzada.
        /// </summary>
        public void AddExperience(int amount)
        {
            Experience += amount;
            while (Experience >= ExperienceRequiredForNextLevel())
            {
                Experience -= ExperienceRequiredForNextLevel();
                LevelUp();
            }
        }

        private int ExperienceRequiredForNextLevel()
        {
            // Fórmula básica de nivelación (esto puede ajustarse según las necesidades del juego)
            return 100 + (Level * 20);
        }

        private void LevelUp()
        {
            Level++;
            MaxHP += 10; // Incrementa la salud máxima al subir de nivel
            MaxMP += 5; // Incrementa el mana máximo al subir de nivel
            CurrentHP = MaxHP; // Restaura la salud al máximo
            CurrentMP = MaxMP; // Restaura el mana al máximo
        }

        /// <summary>
        /// Método para regenerar HP y MP parcialmente (al final de un turno o después de un tiempo).
        /// </summary>
        public void Regenerate(int hpAmount, int mpAmount)
        {
            CurrentHP = Math.Min(MaxHP, CurrentHP + hpAmount);
            CurrentMP = Math.Min(MaxMP, CurrentMP + mpAmount);
        }
    }
}