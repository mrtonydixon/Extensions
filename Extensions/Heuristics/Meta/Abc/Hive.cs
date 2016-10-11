﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Extensions;

namespace Extensions.Heuristics.Meta.Abc
{
    public partial class Hive<FoodType, IBee> : IMetaHeuristic<FoodType>
    {
        public Configuration<FoodType> Config { get; set; }
        //Note that FoodType and FoodSource should be of Same Class/Type
        private FoodType _bestFood { get; set; } // the best food source
        private double _bestFitness { get; set; } // the quality of the best food source
        private int _failureLimit = 20;
        private double _acceptProbability = 0.4; // the probability that an onlooker bee will accept a food source proposed by an employee bee
        //private Func<FoodType, FoodType> _cloneFunc { get; set; } // a function for cloning a food source. This is important to prevent two bees working on the same object in memory at a time
        //private Func<IEnumerable<FoodType>, IEnumerable<double>, int, IEnumerable<FoodType>> _selectionMethod { get; set; }
        //public Search.Direction Movement { get; set; }
        public List<IBee<FoodType>> Bees = new List<IBee<FoodType>>();

        private int _iterationCount = 0;
        private List<double> _iterationFitnessSequence = new List<double>();

        public Hive()
        {

        }

        public Hive(int _failureLimit = 20, double _acceptanceProbability = 0.4)
        {
            this._failureLimit = _failureLimit;
            this._acceptProbability = _acceptanceProbability;
        }

        public void Create(Configuration<FoodType> config)
        {
            this.Config = config;
            int noOfBees = config.PopulationSize;
            if (noOfBees <= 1)
            {
                throw new Exception("You dey Craze? Dem tell you say one (1) bee na Swarm?");
            }
            if (Bees.Count == 0)
            {
                for (int index = 1; index <= noOfBees; index++)
                {
                    BeeTypeClass _type = default(BeeTypeClass);
                    if (index < (noOfBees / 2))
                    {
                        _type = BeeTypeClass.Employed;
                    }
                    else
                    {
                        _type = BeeTypeClass.Onlooker;
                    }
                    IBee<FoodType> bee = (IBee<FoodType>)Activator.CreateInstance<IBee>();
                    bee.Init(config.MutationFunction, config.ObjectiveFunction, _type, index - 1, _failureLimit, config.Movement);
                    Bees.Add(bee);
                }
            }
            this.Start();
        }

        private void Start()
        {
            if (this.Config.Movement == Search.Direction.Optimization)
            {
                _bestFitness = double.MaxValue;
            }
            else if (this.Config.Movement == Search.Direction.Divergence)
            {
                _bestFitness = double.MinValue;
            }
            if (Bees.AsEnumerable().Count(_bee => { return _bee.GetFood() != null; }) == 0)
            {
                for (int index = 0; index <= Bees.Count - 1; index++)
                {
                    IBee<FoodType> _bee = this.Bees[index];
                    _bee.SetBeeID(index);
                    if (_bee.GetBeeType() == BeeTypeClass.Employed)
                    {
                        _bee.SetFood(Config.InitializeSolutionFunction());
                        _bee.GetFitness();
                    }
                }
            }
        }

        public FoodType SingleIteration()
        {
            FoodType ret = default(FoodType);
            List<IBee<FoodType>> _employedBees = Bees.Where((IBee<FoodType> _bee) => { return _bee.GetBeeType() == BeeTypeClass.Employed && _bee.GetFood() != null; }).ToList();
            int _employedCount = _employedBees.Count();
            for (int i = 0; i <= (_employedCount - 1); i++)
            {
                IBee<FoodType> _eBee = _employedBees.ElementAt(i);
                if (_eBee.GetBeeType() == BeeTypeClass.Employed && _eBee.GetFood() != null)
                {
                    FoodType currentFood = Config.CloneFunction.Invoke(_eBee.GetFood());
                    FoodType newFood = _eBee.Mutate();
                    if (Config.HardObjectiveFunction != null)
                    {
                        if (newFood != null)
                        {
                            bool passHardConstraints = Config.HardObjectiveFunction.Invoke(newFood);
                            if ((Config.EnforceHardObjective && passHardConstraints) || passHardConstraints) _eBee.SetFood(newFood);
                            else _eBee.SetFood(currentFood);
                        }
                        else _eBee.SetFood(currentFood);
                    }
                    else if (newFood != null) _eBee.SetFood(newFood);
                    else _eBee.SetFood(currentFood);
                    Bees[_eBee.GetBeeID()] = _eBee;
                }
            }
            this.ShareInformation();
            IEnumerable<double> _fitnesses = Bees.Select((IBee<FoodType> _bee) => { return _bee.GetFitness(); });
            double _bestFit = 0;
            //collate bestFitness and bestFood
            if (this.Config.Movement == Search.Direction.Divergence)
            {
                _bestFit = _fitnesses.Max();
                ret = Bees[_fitnesses.ToList().IndexOf(_bestFit)].GetFood();
                if (_bestFit > _bestFitness)
                {
                    _bestFitness = _bestFit;
                    _bestFood = this.Config.CloneFunction.Invoke(ret);
                }
            }
            else if (this.Config.Movement == Search.Direction.Optimization)
            {
                _bestFit = _fitnesses.Min();
                ret = Bees[_fitnesses.ToList().IndexOf(_bestFit)].GetFood();
                if (_bestFit < _bestFitness)
                {
                    _bestFitness = _bestFit;
                    _bestFood = this.Config.CloneFunction.Invoke(ret);
                }
            }
            if (Config.WriteToConsole && _iterationCount % Config.ConsoleWriteInterval == 0)
            {
                if (Config.ConsoleWriteFunction == null)
                {
                    Console.Write(_iterationCount + "\t" + _bestFitness + "");
                    Console.Write("\t" + _bestFood.ToJson() + "\t");
                    Console.Write("E-Bees: " + _employedCount + '\t');
                    Console.Write("On-Bees: " + Convert.ToInt32(Bees.Count - _employedCount) + '\t');
                    if ((Config.HardObjectiveFunction != null)) Console.Write("Hard: " + Config.HardObjectiveFunction.Invoke(_bestFood));
                    Console.WriteLine();
                }
                else
                {
                    Config.ConsoleWriteFunction(_bestFood, _bestFitness, _iterationCount);
                }
            }
            return ret;
        }

        public List<double> GetIterationSequence()
        {
            return _iterationFitnessSequence;
        }

        public FoodType FullIteration()
        {
            FoodType ret = default(FoodType);
            for (int count = 1; count <= Config.NoOfIterations; count++)
            {
                _iterationCount = count;
                ret = SingleIteration();
                _iterationFitnessSequence.Add(_bestFitness);
            }
            ret = _bestFood;
            Console.WriteLine("End of Iterations");
            return ret;
        }

        public void ShareInformation()
        {
            IEnumerable<IBee<FoodType>> _employedBees = Bees.Where((IBee<FoodType> _bee) => { return _bee.GetBeeType().Equals(BeeTypeClass.Employed); });
            IEnumerable<IBee<FoodType>> _onlookerBees = Bees.Where((IBee<FoodType> _bee) => { return _bee.GetBeeType().Equals(BeeTypeClass.Onlooker); });
            foreach (IBee<FoodType> _bee in _onlookerBees)
            {
                FoodType _food = SelectFood();
                if (_food != null && Number.Rnd() < _acceptProbability)
                {
                    _bee.ChangeToEmployed(_food);
                }
            }
            if (_employedBees.Count() == 0 & _onlookerBees.Count() > 0)
            {
                _onlookerBees.First().ChangeToEmployed(_bestFood);
            }
        }

        public FoodType SelectFood()
        {
            IEnumerable<IBee<FoodType>> _employedBees = Bees.Where((IBee<FoodType> _bee) => { return _bee.GetBeeType().Equals(BeeTypeClass.Employed) & _bee.GetFood() != null; });
            List<double> fitnesses = _employedBees.Select(_bee => { return _bee.GetFitness(); }).ToList();
            double sum = fitnesses.Sum();
            if (fitnesses.IsEmpty()) return default(FoodType);
            return this.Config.SelectionFunction.Invoke(_employedBees.Select((_bee) => _bee.GetFood()), fitnesses, 1).First();
            /*while (true)
            {
                int selectedIndex = 0;
                foreach (double fitness in fitnesses)
                {
                    double probability = fitness / sum;
                    if (Number.Rnd() < probability)
                    {
                        return _cloneFunc(_employedBees.ElementAt(selectedIndex).Food);
                    }
                    selectedIndex += 1;
                }
            }*/
        }
    }
}
