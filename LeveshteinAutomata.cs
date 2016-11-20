using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Levenshtein
{
    /**
     * This class creates Levenshtein Automata based off of provided strings. These automata can then be used
     * to find the closeness (i.e. distance, contains) between the provided string and the compared string at a
     * much faster rate than a straight comparison or Levenshtein algorithm implementation.
     * 
     * General Use Tips:
     *  - Due to being reliant on bit-shifting to produce unique indexes used to identify the nodes, this program
     *  will break will break when it reaches a number higher than its maximum 
     *      e.g. In a 32-bit system, a string of length 32 will cause the program to fail (index starts at 2 so shifting 32 places results in an invalid number).
     *      Every additional error allowed will decreased the maximum number of shifts by 1 (a string of length 29 would fail with 2 errors allowed)
     *  - The algorithm considers capital and lowercase of the same letter to count as a difference in a string. Set all entered strings
     *  to lowercase to avoid faulty results if upper and lowercase letters are considered the same.
     *  - DFAs based on strings that repeat characters may advance several nodes depending on the number of allowed errors 
     *  (e.g. the DFA of string "people" at "o" accepting 3 errors will accept 'e' as an input)  
     */
    public class LevenshteinAutomata
    {
        // Saved automata
        private NFA nfa;
        private DFA dfa;

        /**
         * This class represents a given string as a non-determinstic automata (has many potential states for one input).
         * It is comparable with algorithmic implementions of Levenshtein distance due to this. Is able to be converted
         * to a DFA through a closure operation which makes its nodes deterministic.
         */
        public class NFA
        {
            // Parameters that the NFA will be generated with
            private String term; // The string the NFA was constructed with
            private int numErrors; // The allowable number of deviations from the given string
            private int startState; // The starting state of the NFA

            private int p; // (numErrors + 1). Used for print statements

            // Class Parameters
            private List<int> nodes = new List<int>(); // List of the NFA's nodes
            public Dictionary<int, HashSet<String>> inputs = new Dictionary<int, HashSet<string>>(); // Set of inputs for each node by their index 

            // A mapping of a node's next states to their required inputs. Mapping is linked to the node's index
            public Dictionary<int, Dictionary<string, HashSet<int>>> nextStates = new Dictionary<int, Dictionary<string, HashSet<int>>>();

            // A set of the states that comprise a node's state. Linked by the node's index
            public Dictionary<int, HashSet<int>> states = new Dictionary<int, HashSet<int>>();

            // Basic constructor for the NFA
            public NFA(string term, int numErrors)
            {
                this.term = term;
                this.numErrors = numErrors;
                p = this.numErrors + 1;

                // Load the NFA's nodes based on the term's length and the allowable edit distance
                loadNodes(term.Length, numErrors);

                // Set start state. Should always be at the first node
                setStartState(((2 << (p)) + (2 << 0)));
            }

            #region Setup Functions

            /**
             * Load the nodes of the NFA using the length of the given term and the allowable number of errors
             */
            public void loadNodes(int length, int numErrors)
            {
                nodes = new List<int>(); // Clear the list, in case it was populated before

                int index; // Placeholder for a node's index

                // State Number Loop
                for (int i = 0; i <= length; i++)
                {
                    // Error Number Loop
                    for (int e = 0; e <= numErrors; e++)
                    {
                        index = ((2 << (i + p)) + (2 << e));

                        nodes.Add(index); // Add the index into the NFA's nodes

                        // Create new entries for the index
                        inputs.Add(index, new HashSet<string>());
                        nextStates.Add(index, new Dictionary<string, HashSet<int>>());
                        states.Add(index, new HashSet<int> { index });
                    }
                }
            }

            /**
             * Set the start state of the NFA
             */
            public void setStartState(int index)
            {
                startState = index;
            }

            // Retrieval Functions

            /**
             * Get the number of errors allowed by this NFA
             */
            public int GetAllowableErrors()
            {
                return numErrors;
            }

            /**
             * Get the combined index of all the given nodes
             */
            public int getCombinedIndex(int[] nodeArray)
            {
                int index = 0;

                for (int i = 0; i < nodeArray.Length; i++)
                {
                    index += nodeArray[i];
                }

                return index;
            }

            /**
             * Retrieves all the states that are reachable from the given states by an "any" transition
             */
            public HashSet<int> GetDefaultStates(HashSet<int> stateSet)
            {
                var node = stateSet.ToArray();

                HashSet<int> anyStates;

                for (int i = 0; i < node.Length; i++)
                {
                    if (nextStates[node[i]].TryGetValue("any", out anyStates))
                    {
                        stateSet.UnionWith(GetDefaultStates(anyStates));
                    }
                }

                return stateSet;
            }

            /**
             * Gets the inputs of the node represented by the given index
             */
            public HashSet<string> GetInputs(int index)
            {
                var inputSet = new HashSet<string>();

                if (inputs.TryGetValue(index, out inputSet))
                    return inputSet;

                // If the index is not in this NFA, return null
                return null;
            }

            /**
             * Get the possible inputs of all the states in the given set
             */
            public HashSet<string> GetInputs(IEnumerable<int> stateSet)
            {
                var node = stateSet.ToArray();
                var input = new HashSet<string>();

                for (int i = 0; i < node.Length; i++)
                {
                    input.UnionWith(inputs[node[i]]);
                }

                return input;
            }

            /**
             * Retrieve this NFA's nodes
             */
            public List<int> getNodes()
            {
                return nodes;
            }

            /**
             * Retrieve the NFA's starting state
             */
            public int GetStartState()
            {
                return startState;
            }

            /**
             * Get all the states linked to this index
             */
            public HashSet<int> GetStates(int index)
            {
                var stateSet = new HashSet<int>();

                if (states.TryGetValue(index, out stateSet))
                    return stateSet;

                return null;
            }

            /**
             * Gets the resulting state by combining all the states that are associated with the given list of indexes
             */
            /*public HashSet<int> GetStates(int[] indexes)
            {
                var state = new HashSet<int>();
                HashSet<int> stateSet;

                for (int i = 0; i < indexes.Length; i++)
                {
                    if (states.TryGetValue(indexes[i], out stateSet))
                        state.UnionWith(stateSet);
                }

                return state;
            }*/

            /**
             * Retrieves the term used to construct this NFA
             */
            public string GetTerm()
            {
                return term;
            }

            #endregion Setup Functions

            #region Main Functions

            /**
             * Adds a node's information into the database. Confirms that hasn't already been added.
             */
            public void addNode(int index, HashSet<string> nInputs, HashSet<int> nState, Dictionary<string, HashSet<int>> nNextStates)
            {
                HashSet<string> inputSet;
                HashSet<int> stateSet;
                Dictionary<string, HashSet<int>> nextStateDict;

                // Add the updated next state to the NFA's information
                // Note: If it exists, it has already been added as part of another closure
                if (!inputs.TryGetValue(index, out inputSet))
                    inputs.Add(index, nInputs);
                if (!states.TryGetValue(index, out stateSet))
                    states.Add(index, nState);
                if (!nextStates.TryGetValue(index, out nextStateDict))
                    nextStates.Add(index, nNextStates);
            }

            /**
             * Add a transition from one node to another with a given input.
             */
            public void addTransition(int start, string input, int end)
            {
                // Get all the next states of the starting node
                var nextStateDict = nextStates[start];

                HashSet<int> inputStates; // Placeholder for the starting nodes next states at the given input

                // If the node already has a transition for this given input, add the end node to its next states
                if (nextStateDict.TryGetValue(input, out inputStates))
                    inputStates.Add(end);
                // Else, add a new transition on this input
                else
                {
                    inputs[start].Add(input); // Add the input to the node's inputs
                    nextStateDict.Add(input, new HashSet<int> { end }); //Add the transition
                }

                //inputs[start].Add(input);
            }

            /**
             * (Unimplemented) Calculates the value of the given index under closure. 
             */
            public int calculateClosedIndex(int index)
            {
                var numErrors = index % 10;
                var num = (index - numErrors) * 0.10;

                for (double i = num; i < (num + numErrors); i++)
                {

                }

                return 0;
            }

            /**
             * Converts the NFA to a DFA by making "any" transitions part of the state and updating the nodes' transitions accordingly.
             * Overwrites the current values for the nodes, so they will have to be backed up if you want to preserve the original NFA.
             */
            public int closure(int index, HashSet<int> sState)
            {
                // Tables that store the node's information after closure
                var sInputs = new HashSet<string>();
                var sStates = new HashSet<int>(sState);
                var sNextStates = new Dictionary<string, HashSet<int>>();

                string[] input;

                // Get all the states that can be reached from an "any" transition
                sStates.UnionWith(GetDefaultStates(sStates));

                // Reset the index now that the states have merged with their "any" transitions
                index = getCombinedIndex(sStates.ToArray());

                // CHECK IF IT HAS ALREADY BEEN CLOSED!!!
                if (states.ContainsKey(index))
                    return index;

                // Get all the inputs for these nodes
                sInputs = GetInputs(sStates);

                // Merge their next states
                sNextStates = MergeNextStates(sInputs, new HashSet<int>(sStates)); // Make a copy/new set so original will be unaffected by operations

                // Loop through the node's inputs and update its transitions.
                input = sInputs.ToArray();

                for (int i = 0; i < input.Length; i++)
                {
                    var nState = sNextStates[input[i]]; // Get the states reachable from this input
                    int newIndex = getCombinedIndex(nState.ToArray()); // Get the combined index of these states

                    newIndex = closure(newIndex, nState); // Recursively call closure on the index (will add "any" transitions to it)

                    sNextStates[input[i]] = new HashSet<int> { newIndex }; // Sets the new, closed, index as the next state of this input
                }

                // Add the node if it is new (should be at this point, otherwise would have hit the check)
                addNode(index, sInputs, sStates, sNextStates);

                return index;
            }

            /**
             * Generates a set of next states by combining the next states of a set of states that have inputs within
             * the given set of inputs
             */
            public Dictionary<string, HashSet<int>> MergeNextStates(HashSet<string> inputSet, HashSet<int> stateSet)
            {
                var input = inputSet.ToArray();
                var sNextStates = new Dictionary<string, HashSet<int>>();

                // Initialize the next state list by adding an entry for each given input
                for (int i = 0; i < input.Length; i++)
                {
                    sNextStates.Add(input[i], new HashSet<int>());
                }

                int node;

                // Loop through the state set and add all the next states of each state into sNextStates
                while (stateSet.Any())
                {
                    node = stateSet.First();
                    stateSet.Remove(node);

                    input = nextStates[node].Keys.ToArray(); // Set of inputs for the given node 
                                                            // (should be within the given input set so next states return should have an entry)

                    for (int i = 0; i < input.Length; i++)
                    {
                        sNextStates[input[i]].UnionWith(nextStates[node][input[i]]);
                    }
                }

                return sNextStates;
            }

            /** Performs closure on all the nodes starting from the NFA's starting node and assigns the
             * new nodes and transitions to the DFA
             */
            public void PerformClosure(DFA dfa)
            {
                // Save copies of the NFA's values as they will be modified for the closure
                var savedInputs = inputs;
                var savedStates = states;
                var savedNextStates = nextStates;

                var nodeSet = new HashSet<int> { startState };

                // Declare the first node as the start state under closure
                int frontier = closure(startState, states[startState]);

                // List of nodes by their indexes that signifies that the node has already been added
                var seen = dfa.nodes;

                // Add the first node and set it to the start stae
                //seen.Add(frontier);
                dfa.setStartState(frontier);

                // Placeholders for output methods
                HashSet<int> nextStateSet;
                int[] nextStatesArray;

                string[] inputArray;

                // List of node indexes obtained by adding the next states of the current frontier
                HashSet<int> nodeStack = new HashSet<int> { frontier };

                // Loop while frontier is not null. At the end of loop, set the frontier to next node on the stack
                while (frontier != null)
                {
                    nodeStack.Remove(frontier); // Remove the node from the stack

                    seen.Add(frontier); // Add the node to the DFA's node

                    // Add the information (inputs, state, next states) for this node
                    dfa.inputs.Add(frontier, inputs[frontier]);
                    dfa.states.Add(frontier, states[frontier]);
                    dfa.nextStates.Add(frontier, nextStates[frontier]);

                    inputArray = inputs[frontier].ToArray(); // Array of inputs for this node

                    // Loop through the inputs of this node and get all new/unseen nodes that are reachable
                    for (int i = 0; i < inputArray.Length; i++)
                    {
                        nextStatesArray = nextStates[frontier][inputArray[i]].ToArray();

                        var except = nextStatesArray.Except(seen);

                        // If there are new nodes, add them to the stack
                        if (except != null)
                            nodeStack.UnionWith(except);
                    }

                    frontier = nodeStack.FirstOrDefault(); // Set the frontier to the next node on the stack

                    // If there are no nodes on the stack, exit the loop
                    if (frontier == 0)
                        break;
                }

            }

            #endregion Main Functions

            #region Print Functions

            /**
             * Get the power (of 2) this index is raised to.
             */
            public int GetPower(int index)
            {
                int power = 0;
                while ((2 << power) < index)
                    power++;

                return power;
            }

            public string NodeToString(int index)
            {
                var inputStr = "";
                var inputArray = inputs[index].ToArray();

                var nextStateDict = nextStates[index];

                var nextStatesStr = "";

                int[] stateArray;

                for (int i = 0; i < inputArray.Length; i++)
                {
                    inputStr += inputArray[i] + ", ";

                    stateArray = nextStateDict[inputArray[i]].ToArray();

                    for (int j = 0; j < stateArray.Length; j++)
                    {
                        nextStatesStr += "(" + inputArray[i] + " -> " + PrintState(stateArray[j]) + ")" + ", ";
                    }

                }

                if (inputStr.Length > 2)
                    inputStr = inputStr.Substring(0, inputStr.Length - 2);
                if (nextStatesStr.Length > 1)
                    nextStatesStr = nextStatesStr.Substring(0, nextStatesStr.Length - 2);

                string printStmt = PrintState(index) + '\n'
                                   + "Index: " + index + '\n'
                                   + "Inputs: " + inputStr + '\n'
                                   + "Next States: " + nextStatesStr;

                return printStmt;
            }

            public string PrintState(int index)
            {
                var numErrors = 0;
                var num = 0.0;
                var power = GetPower(index);

                string stateStr = "";

                if (states[index].Count() == 1)
                {
                    num = power - (p + 1);
                    numErrors = GetPower((int)(index - Math.Pow(2, power)));


                    string state = "<" + num + "," + numErrors + ">";

                    return state;
                }

                stateStr += "{";

                var stateArray = states[index].ToArray();

                int indice;

                for (int i = 0; i < stateArray.Length; i++)
                {
                    indice = stateArray[i];
                    power = GetPower(indice);

                    num = power - (p + 1);
                    numErrors = GetPower((int)(indice - Math.Pow(2, power)));

                    stateStr += "<" + num + "," + numErrors + ">";
                }

                return stateStr + "}";
            }

            override public string ToString()
            {
                string nfaString = "";

                foreach(int node in nodes)
                {
                    nfaString += (NodeToString(node) + "\n");
                }

                return nfaString;
            }

            #endregion Print Functions

        }
        
        /**
         * This class represents a given string as a determenistic automata. The string usually starts as a NFA and must
         * undergo closure in order to convert into a DFA. Achieves faster comparison times than a NFA when 
         * construction (i.e. closure) and transversal of the DFA is less than a transversal of the NFA.
         * 
         * Due to its ability to transverse a string as an automata, can answer questions such as containment of one string
         * in another with some degree of error allowed (e.g. "zzzapplezzz" would be known to contain "apple") 
         */
        public class DFA
        {
            // Parameters that the NFA will be generated with
            private String term;
            private int numErrors;
            private int startState;

            private int p;

            public List<int> nodes = new List<int>();
            public Dictionary<int, HashSet<int>> states = new Dictionary<int, HashSet<int>>();
            public Dictionary<int, HashSet<String>> inputs = new Dictionary<int, HashSet<string>>();
            public Dictionary<int, Dictionary<string, HashSet<int>>> nextStates = new Dictionary<int, Dictionary<string, HashSet<int>>>();

            /**
             * Basic constructor for the DFA
             */
            public DFA(string term, int numErrors)
            {
                this.term = term;
                this.numErrors = numErrors;

                p = numErrors + 1;
                startState = 210;
            }

            /**
             * Method to check whether or not the DFA has reached a final state
             */
            public Boolean IsFinal(int index)
            {
                var stateSet = states[index];
                var maxIndex = (2 << ((term.Length) + p));
                var finalSet = new HashSet<int>();

                var e = 0;
                int error;

                while (e <= numErrors)
                {
                    error = 2 << e;

                    finalSet.Add(maxIndex + error);

                    //if (stateSet.Contains(maxIndex + error))
                    //return true;

                    e++;
                }

                if (stateSet.Intersect(finalSet).Any())
                    return true;

                return false;
            }

            /**
            * Set the start state of the DFA
            */
            public void setStartState(int index)
            {
                startState = index;
            }

            /**
             * Tranverse through the DFA and return the number of differences between the compared string (word)
             * and the string the DFA represents (term)
             */
            public int Transversal(string word)
            {
                var nodeArray = nodes.ToArray();

                var frontier = startState;
                var errors = 0;
                var distance = 0;

                int i = 0;
                char ch;

                while (i < word.Length)
                {
                    //Console.WriteLine(frontier + " -> ");
                    ch = word[i];

                    if (inputs[frontier].Contains(word[i].ToString()))
                        frontier = nextStates[frontier][word[i].ToString()].First();
                    else if (inputs[frontier].Contains("any"))
                    {
                        errors++;
                        frontier = nextStates[frontier]["any"].First();
                    }
                    else
                    {
                        errors++;
                    }

                    i++;

                    if (errors > numErrors)
                        return -1;
                }

                if (IsFinal(frontier))
                    return errors;

                return -1;
            }

            /**
             * Tranverse through the DFA and return the number of differences between the compared string (word)
             * and the string the DFA represents (term). Accepts the containment of the DFA's string within the
             * given string as an valid result.
             */
            public int TransversalContains(string word)
            {
                var nodeArray = nodes.ToArray();

                var frontier = startState;
                var errors = 0;
                var distance = 0;

                int i = 0;
                //char ch; // Test character

                // Loop through each character in the word
                while (i < word.Length)
                {
                    //ch = word[i];

                    // If the node accepts the character as an input, transition to the next node
                    if (inputs[frontier].Contains(word[i].ToString()))
                    {
                        frontier = nextStates[frontier][word[i].ToString()].First();
                    }
                    // Else, if matching has started (not in starting state), check if the
                    else if (frontier != startState)
                    {
                        // 
                        if (IsFinal(frontier))
                        {
                            if ((i + 1) == word.Length)
                                return errors;
                            
                            if (word[i] == ')' || word[i] == ' ' || word[i] == '/' || word[i] == '-')
                                return errors;

                            return -1;
                        }
                        else
                        {
                            frontier = startState;
                        }
                    }

                    i++;
                }

                if (IsFinal(frontier))
                    return errors;

                return -1;
            }

            #region Unused Transversal Methods

            /**
             * Traverse through the DFA and return the number of differences between the strings. Accepts a certain
             * amount of error between the compared string and the string the DFA represents as well as accepting
             * containment of the DFA's string inside the compared string as a valid result.
             * 
             * Discouraged in general string comparison as the acceptance of error as well as containment at the same time
             * often leads to matches that are not intuitive or desired. Encouraged when comparing one string to a small set
             * as that reduces the chance that it will match to something wholly inappropriate.
             */
            public int TransversalL(string word)
            {
                var nodeArray = nodes.ToArray();

                var frontier = startState;
                var errors = 0;
                var distance = 0;

                int i = 0;
                char ch;

                while (i < word.Length)
                {
                    ch = word[i];

                    if (inputs[frontier].Contains(word[i].ToString()))
                    {
                        frontier = nextStates[frontier][word[i].ToString()].First();
                    }
                    else if (frontier != startState)
                    {
                        if (inputs[frontier].Contains("any"))
                        {
                            errors++;
                            frontier = nextStates[frontier]["any"].First();
                        }
                        else if (IsFinal(frontier))
                        {
                            //if ((i + 1) == word.Length)
                              //  return errors;

                            //if (word[i] == ')' || word[i] == ' ' || word[i] == '/' || word[i] == '-')
                             //   return errors;

                            return word.Length - term.Length;
                        }
                        else
                        {
                            frontier = startState;
                        }
                    }
                    else
                    {
                        errors++;
                    }

                    i++;
                }

                if (IsFinal(frontier))
                    return errors;

                return -1;
            }

            /**
             * Transversal method that accepts strings that start with the same string as the DFA with a given
             * amount of error. Did not see as many cases to use compared to containment so not supported.
             */
            public int TransversalStartWith(string word)
            {
                var nodeArray = nodes.ToArray();

                var frontier = startState;
                var errors = 0;
                var distance = 0;

                int i = 0;
                char ch;

                while (i < term.Length)
                {
                    ch = word[i];

                    if (inputs[frontier].Contains(word[i].ToString()))
                        frontier = nextStates[frontier][word[i].ToString()].First();
                    else if (inputs[frontier].Contains("any"))
                    {
                        errors++;
                        frontier = nextStates[frontier]["any"].First();
                    }
                    else
                    {
                        errors++;
                    }

                    if (errors > numErrors)
                        return -1;

                    i++;

                    if (i == word.Length)
                    {
                        // If the compared word's length can be reached with the allowed amount of errors, return the number of errors
                        if (term.Length < (word.Length + (numErrors - errors)))
                            return term.Length - word.Length;
                        else if (term.Length > (word.Length + (numErrors - errors)))
                        {
                            return -1;
                        }
                        else
                        {
                            return errors;
                        }
                    }
                }

                if (term.Length == word.Length)
                    return errors;
                if (term.Length < word.Length)
                    if (IsFinal(frontier))
                        return p + errors;
                // return word.Length - term.Length

                return -1;
            }

            #endregion Unused Tranversal Methods

            #region Print Functions

            /**
             * Get the power (of 2) this index is raised to. (Printing)
             */
            public int GetPower(int index)
            {
                int power = 0;
                while ((2 << power) < index)
                    power++;

                return power;
            }

            public String NodeToString(int index)
            {
                var inputStr = "";
                var inputArray = inputs[index].ToArray();

                var nextStateDict = nextStates[index];

                var nextStatesStr = "";

                int[] stateArray;

                for (int i = 0; i < inputArray.Length; i++)
                {
                    inputStr += inputArray[i] + ", ";

                    stateArray = nextStateDict[inputArray[i]].ToArray();

                    for (int j = 0; j < stateArray.Length; j++)
                    {
                        nextStatesStr += "(" + inputArray[i] + " -> " + PrintState(stateArray[j]) + ")" + ", ";
                    }

                }

                if (inputStr.Length > 2)
                    inputStr = inputStr.Substring(0, inputStr.Length - 2);
                if (nextStatesStr.Length > 1)
                    nextStatesStr = nextStatesStr.Substring(0, nextStatesStr.Length - 2);

                string printStmt = PrintState(index) + '\n'
                                   + "Index: " + index + '\n'
                                   + "Inputs: " + inputStr + '\n'
                                   + "Next States: " + nextStatesStr;

                return printStmt;
            }

            public String PrintState(int index)
            {
                var numErrors = 0;
                var num = 0.0;
                var power = GetPower(index);

                string stateStr = "";

                if (states[index].Count() == 1)
                {
                    num = power - (p + 1);
                    numErrors = GetPower((int)(index - Math.Pow(2, power)));


                    string state = "<" + num + "," + numErrors + ">";

                    return state;
                }

                stateStr += "{";

                var stateArray = states[index].ToArray();

                int indice;

                for (int i = 0; i < stateArray.Length; i++)
                {
                    indice = stateArray[i];
                    power = GetPower(indice);

                    num = power - (p + 1);
                    numErrors = GetPower((int)(indice - Math.Pow(2, power)));

                    stateStr += "<" + num + "," + numErrors + ">";
                }

                return stateStr + "}";
            }

            override public string ToString()
            {
                string nfaString = "";

                foreach (int node in nodes)
                {
                    nfaString += (NodeToString(node) + "\n");
                }

                return nfaString;
            }

            #endregion Print Functions
        }

        /**
         * Basic constructor for the class. Initializes the NFA, calls the closure method.
         */
        public LevenshteinAutomata(string term, int numErrors)
        {
            this.nfa = new NFA(term, numErrors);
            this.dfa = new DFA(term, numErrors);

            int startIndex;
            int endIndex;

            int p = numErrors + 1;

            // Create the NFA based on the given term and number of errors allowed
            // Note: Target length checks are so can add any transitions
            for (int i = 0; i <= term.Length; i++)
            {
                for (int e = 0; e <= numErrors; e++)
                {
                    startIndex = ((2 << (i + p)) + (2 << e));
                    endIndex = ((2 << (i + (p + 1))) + (2 << e));
                    // Correct Character
                    if (i < term.Length)
                        nfa.addTransition(startIndex, term[i] + "", endIndex);

                    if (e < numErrors)
                    {
                        // Deletion/Insertion
                        nfa.addTransition(startIndex, "any", ((2 << (i + p)) + (2 << (e + 1))));

                        if (i < term.Length)
                        {
                            // Insertion
                            //nfa.addTransition(startIndex, "epsilon", endIndex + 1);
                            // Substitution
                            nfa.addTransition(startIndex, "any", ((2 << (i + (p + 1))) + (2 << (e + 1))));
                        }
                    }
                }
            }

            // Perform closure on the NFA and put the result in the DFA
            nfa.PerformClosure(dfa);
        }

        /**
         * Test to see if the NFA was generated correctly
         */
        public static Boolean NFAGenerationTest(NFA nfa)
        {
            // Min./Max. Index Test    
            // All node indexes are less than the terms maximum index
            var maxErrors = nfa.GetAllowableErrors();
            var p = maxErrors + 1;

            var minIndex = nfa.GetStartState();
            var maxIndex = (2 << (nfa.GetTerm().Length + p)) + (2 << maxErrors);

            var nodes = nfa.getNodes().ToArray();

            // Each node must be less than or equal to the maximum index in order to pass
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] < minIndex)
                {
                    Console.WriteLine("Minimum Index Test Failed: " + "\n" + "\t" +
                        "Node Index: " + nodes[i] + " is under the minimum index of " + minIndex);
                    return false;
                }
                else if (nodes[i] > maxIndex)
                {
                    Console.WriteLine("Maximum Index Test Failed: " + "\n" + "\t" +
                        "Node Index: " + nodes[i] + " is over the maximum index of " + maxIndex);
                    return false;
                }
            }

            Console.WriteLine("Min./Max. Index Test Passed");

            // Max Errors Test
            int numErrors = 0;
            int num = 0;
            int power;

            for (int i = 0; i < nodes.Length; i++)
            {
                power = nfa.GetPower(nodes[i]);
                numErrors = nfa.GetPower((int)(nodes[i] - Math.Pow(2, power)));
                if (numErrors > maxErrors)
                {
                    Console.WriteLine("Max Errors Test Failed: " + "\n" + "\t" +
                        "Node Index: " + nodes[i] + " has " + ((nodes[i] % 10) - maxErrors)
                        + " more error(s) than is allowed (" + maxErrors + ")");
                    return false;
                }
            }

            Console.WriteLine("Max Errors Test Passed");

            // Valid Next States

            var index = nfa.nextStates.Keys.ToArray();
            string[] input;
            int[] stateSet;

            for (int i = 0; i < index.Length; i++)
            {
                power = nfa.GetPower(index[i]);
                num = power - (p + 1);
                numErrors = nfa.GetPower((int)(nodes[i] - Math.Pow(2, power)));

                input = nfa.nextStates[index[i]].Keys.ToArray();

                for (int j = 0; j < input.Length; j++)
                {
                    stateSet = nfa.nextStates[index[i]][input[j]].ToArray();

                    for (int k = 0; k < stateSet.Length; k++)
                    {
                        var test = (2 << power) + (2 << (numErrors + (maxErrors - numErrors)));
                        if (stateSet[k] > test)
                        {
                            Console.WriteLine("Valid Next States Test Failed: " + "\n" + "\t" +
                                "Node Index: " + index[i] + " has a next state of " + stateSet[k]);
                            return false;
                        }
                    }
                }
            }

            Console.WriteLine("Valid Next States Passed");

            Console.WriteLine("NFA Generation Tests Passed");

            return true;
        }

        /**
         * Test to see if the NFA was successfully converted into a valid DFA
         */
        public static Boolean NFAClosureTest(NFA nfa, DFA dfa)
        {
            // 1 Output to 1 Input Test
            var index = nfa.nextStates.Keys.ToArray();
            string[] input;
            int nextStateCount;

            int num = 0;
            int numErrors = 0;

            var node = dfa.nodes.ToArray();

            // For each transition on every node in the DFA, confirm that there is a 1-to-1 Input/Output for each
            // transition
            for (int i = 0; i < node.Length; i++)
            {
                input = dfa.nextStates[node[i]].Keys.ToArray();

                for (int j = 0; j < input.Length; j++)
                {
                    nextStateCount = dfa.nextStates[node[i]][input[j]].Count();

                    if (nextStateCount > 1)
                    {
                        Console.WriteLine("1-to-1 Input/Output Test Failed: " + "\n" + "\t" +
                        "Node Index: " + node[i] + " has " + nextStateCount + " transitions on input" + "\n" + "\t"
                        + input[j]);
                        return false;
                    }
                }
            }

            Console.WriteLine("1-to-1 Input/Output Test Passed");

            // Proper States Test

            // Ensure that all the states in the DFA are comprised of states that existed in the NFA

            int[] stateArray;
            int maxIndex = ((nfa.GetTerm().Length + 1) * 10) + nfa.GetAllowableErrors();
            int minIndex = 10;

            for (int i = 0; i < node.Length; i++)
            {
                stateArray = dfa.states[node[i]].ToArray();

                for (int j = 0; j < stateArray.Length; j++)
                {
                    if (stateArray[j] > maxIndex || stateArray[j] < minIndex)
                    {
                        Console.WriteLine("Proper States Test Failed: Index:" + node[i]);
                        return false;
                    }

                }
            }

            Console.WriteLine("Proper States Test Passed");

            // Any Transition Test

            // For the resulting DFA of a NFA under closure, confirm that all the information of the "any"
            // next states is incorporated in the new node

            /*HashSet<string> inputs = new HashSet<string>();
            int[] stateArray;
            Dictionary<string, HashSet<int>> nextStates = new Dictionary<string, HashSet<int>>();

            HashSet<string> cInputs;
            HashSet<int> cStates;
            Dictionary<int, Dictionary<string, HashSet<int>>> cNextStates = nfa.nextStates;

            var nodes = nfa.getNodes().ToArray();
            var cNodes = dfa.nodes.ToArray();

            for (int i = 0; i < cNodes.Length; i++)
            {
                stateArray = nfa.states[cNodes[i]].ToArray();

                // For each node in this closed index,
                for (int j = 0; j < stateArray.Length; j++)
                {
                    inputs.UnionWith(nfa.inputs[stateArray[j]]);
                    nfa.mergeNextStates(nextStates, nfa.nextStates[stateArray[j]]);
                }
                
                // Now that the next states are filled, can test to see if they are identical to the closed node's

                // First check that the inputs are the same
                if (inputs == nfa.inputs[cNodes[i]])
                {
                    // For each input, confirm that their next states are identical
                    input = inputs.ToArray();

                    for (int j = 0; j < input.Length; j++)
                    {
                        var indice = nfa.getCombinedIndex(nextStates[input[j]].ToArray());

                        if (indice != nfa.getCombinedIndex(cNextStates[cNodes[i]][input[j]].ToArray()))
                        {
                            // Test fails
                            return false;
                        } 
                    }
                }
                else
                {
                    // Test Fails
                    return false;
                }
            }*/

            return true;
        }

        /**
         * Call on the DFA to perform a tranversal with the given string
         */
        public int Transversal(string term)
        {
            var distance =  dfa.Transversal(term);

            return distance;
        }

        /**
         * Calls on the DFA to perform a containment transversal with the given string
         */
        public int TransversalContains(string term)
        {
            var distance = dfa.TransversalContains(term);

            return distance;
        }

        static void Main(string[] args)
        {
            // Create new stopwatch
            Stopwatch stopwatch = new Stopwatch();

            // Begin timing
            stopwatch.Start();

            //String term = NameMatch.Clean("13-WHAM TV");

            LevenshteinAutomata la = new LevenshteinAutomata("microsoft", 1);

            var nfa = la.nfa;

            Console.Write(nfa.ToString());

            var dfa = new DFA("microsoft", 1);

            // DFA Transversal Test

            nfa.PerformClosure(dfa);

            Console.WriteLine(dfa.Transversal("micrtosoft"));

            // Get the elapsed time as a TimeSpan value.
            TimeSpan ts = stopwatch.Elapsed;

            // Format and display the TimeSpan value. 
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:000}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds);
            
            Console.WriteLine("RunTime " + elapsedTime);
        }
    }
}
