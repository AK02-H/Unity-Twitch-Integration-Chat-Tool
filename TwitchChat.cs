/// Twitch Integration Chat Input
/// By A.K
///
/// A script that connects to a Twitch channel stream to read chat messages
/// BUILT-IN FEATURES:
/// -Connects to your Twitch livestream
/// -Adds chat messages to a list
/// -Can return chat's most or least common response from a list of possible answers
/// -Option to only allow one answer per viewer
/// -Choice of different handling methods for when there are tied chat results (even though this should be pretty rare anyway)
/// -Can display the Twitch chat on a UI text element

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.ComponentModel;
using System.Net.Sockets;
using System.IO;
using Random = UnityEngine.Random;

public enum ModeConflictHandleMethod{Random, FirstOption, FastestReached}
public class TwitchChat : MonoBehaviour
{
    #region Variables
    
    //Twitch integration tools
    private TcpClient _twitchClient;
    private StreamReader _reader;
    private StreamWriter _writer;

    [Header("Twitch Channel Details")]
    //Set these in the inspector
    public string username;
    public string password;                    //Receive the password/token for your Twitch account from https://twitchapps.com/tmi/
    public string channelName;
    
    [Header("Lists")]
    public List<String> storedChatAll = new List<string>();
    public List<String> storedChatTemp = new List<string>();
    private List<String> _submittedViewers = new List<string>();
    
    [Header("Script Behaviours")]
    [Tooltip("Set true to only allow one response from each unique Twitch viewer per cycle. \nRecommended you set to false while testing the game.")]
    [SerializeField] private bool filterMultipleResponses;
    [Tooltip("How to handle the conflict of the most frequent Twitch chat answer having multiple instances. \n" +
             "RANDOM: return a random option from the modal values. \n" +
             "FIRST OPTION: prioritise the option that comes first in the array of possible options. \n" +
             "FASTEST REACHED: return the modal value that reached it's count in the Twitch chat the earliest.")]
    public ModeConflictHandleMethod modeConflictHandleType = ModeConflictHandleMethod.Random;

    [Header("Test Tools")]
    //Text elements for debug-displaying the Twitch chat, interval timer and common chat answer
    [SerializeField] private Text _chatText; 
    [SerializeField] private Text _timerText;
    [SerializeField] private Text _inputDisplayText;
    [SerializeField] private float _intervalLength = 20;
    private float _intervalTimer;
    //An array to test Twitch chat responses, set this to be a list of possible options the chat can 'choose' from
    [SerializeField] private string[] _testArray = new string[] {"1", "2", "3"};

    #endregion
    
    #region Event Functions
    
    private void Start()
    {
        Connect();
        _intervalTimer = _intervalLength;
    }
    
    private void Update()
    {
        if (!_twitchClient.Connected)
        {
            Connect();
        }

        ReadChat();
        
        //Return the most commonly chosen value from the test array by the Twitch chat every period of time.
        //After the timer has passed, it resets and clears the periodic chat store.
        
        _intervalTimer -= Time.deltaTime;
        if(_timerText) _timerText.text = _intervalTimer.ToString();
        
        if (_intervalTimer <= 0)
        {
            string answer = ReturnCommonInput(_testArray);
            print(answer);
            if(_inputDisplayText) _inputDisplayText.text = answer;
            
            ResetAnswers();
            
            _intervalTimer = _intervalLength;
        }
    }
    
    #endregion
    
    #region Twitch Functions
    
    //Connect to Twitch stream
    private void Connect()
    {
        _twitchClient = new TcpClient("irc.chat.twitch.tv", 6667);
        _reader = new StreamReader(_twitchClient.GetStream());
        _writer = new StreamWriter(_twitchClient.GetStream());

        _writer.WriteLine("PASS " + password);
        _writer.WriteLine("NICK " + username);
        _writer.WriteLine("USER " + username + " 8 * :" + username);
        _writer.WriteLine("JOIN #" + channelName);
        _writer.Flush();
    }

    //Reads Twitch chat messages and stores them in the appropriate lists
    private void ReadChat()
    {
        if (_twitchClient.Available > 0)
        {
            //Receive latest line from the Twitch chat
            string chatLine = _reader.ReadLine();

            if (chatLine.Contains("PRIVMSG"))
            {
                //Split string into chat name and chat message
                int splitPoint = chatLine.IndexOf("!", 1);
                string chatName = chatLine.Substring(0, splitPoint); 
                chatName = chatName.Substring(1);
                splitPoint = chatLine.IndexOf(":", 1);
                string message = chatLine.Substring(splitPoint + 1);
                
                if (filterMultipleResponses)
                {
                    if (!IfViewerHasAnswered(chatName))
                    {
                        if (IfExistsInArray(message, _testArray))
                        {
                            _submittedViewers.Add(chatName);
                            storedChatTemp.Add(message.ToLower().Trim());
                        }
                        
                        storedChatAll.Add(String.Format("{0}: {1}", chatName, chatLine));
                
                        print(String.Format("{0}: {1}", chatName, message));
                        
                        if(_chatText)
                            _chatText.text = _chatText.text + "\n" + String.Format("{0}: {1}", chatName, message);
                    }
                    //if the user has already answered for this loop, do nothing
                }
                else
                {
                    storedChatTemp.Add(message.ToLower().Trim());
                
                    print(String.Format("{0}: {1}", chatName, message));
                    
                    if(_chatText)
                        _chatText.text = _chatText.text + "\n" + String.Format("{0}: {1}", chatName, message);
                }
            }
        }
    }
    
    
    
    ///HOW TO USE THIS FUNCTION:
    ///Pass in the available options for the chat to choose from as an array of strings
    ///The function will return the 'option' string that was the most common in the currently stored Twitch chat
    public string ReturnCommonInput(string[] options)
    {
        int optionCount = options.Length;
        List<int> values = new List<int>();

        foreach (var op in options)
        {
            values.Add(0);
            foreach (var s in storedChatTemp)
            {
                if (op == s)
                {
                    values[Array.IndexOf(options, op)]++;
                }
            }
        }
        
        int highestValue = LargestFromList(values);
        
        int index;
        string toReturn;
        
        int modeCount = 0;    //how many modal values exist in values

        foreach (int i in values)
        {
            if (i == highestValue)
                modeCount++;
        }

        if (modeCount > 1)
        {
            switch (modeConflictHandleType)
            {
                case ModeConflictHandleMethod.Random:
                    
                    List<int> modeIndexes = new List<int>();
                    
                    for (int i = 0; i < values.Count; i++)
                    {
                        if (values[i] == highestValue)
                        {
                            modeIndexes.Add(i);
                        }
                    }
                
                    index = modeIndexes[Random.Range(0, modeIndexes.Count)];
                    break;
                
                case ModeConflictHandleMethod.FirstOption:
                    
                    index = values.IndexOf(highestValue);
                    break;
                
                case ModeConflictHandleMethod.FastestReached:
                    
                    List<string> popularOptions = new List<string>();
            
                    List<int> modeIndexes2 = new List<int>();
                    for (int i = 0; i < values.Count; i++)
                    {
                        if (values[i] == highestValue)
                        {
                            modeIndexes2.Add(i);
                        }
                    }

                    for (int i = 0; i < modeIndexes2.Count; i++)
                    {
                        popularOptions.Add(options[modeIndexes2[i]]);
                    }

                    int[] latestOccurence = new int[popularOptions.Count];
                    
                    for (int i = 0; i < popularOptions.Count; i++)
                    {
                        foreach (string VARIABLE2 in storedChatTemp)
                        {
                            if (popularOptions[i] == VARIABLE2)
                            {
                                latestOccurence[i] = Array.IndexOf(storedChatTemp.ToArray(), VARIABLE2);
                            }
                        }
                    }
                    
                    int lowest = Mathf.Min(latestOccurence);
                    int h = Array.IndexOf(latestOccurence, lowest);
                    string s = popularOptions[h];

                    index = Array.IndexOf(options, s);
                    break;
                
                default:
                    //Runs FirstOption code by default, though this line should be impossible to reach
                    index = values.IndexOf(highestValue);
                    break;
            }
        }
        else
        {
            index = values.IndexOf(highestValue);
        }
        
        toReturn = options[index].ToString();

        return toReturn;
    }
    
    ///Works the same as ReturnCommonInput, except it returns the option that was the least common in the Twitch chat
    public string ReturnRareInput(string[] options)
    {
        int optionCount = options.Length;
        List<int> values = new List<int>();

        foreach (var op in options)
        {
            values.Add(0);
            foreach (var s in storedChatTemp)
            {
                if (op == s)
                {
                    values[Array.IndexOf(options, op)]++;
                }
            }
        }
        
        int lowestValue = SmallestFromList(values);

        int index;
        string toReturn;
        
        int modeCount = 0;    //how many modal values exist in values

        foreach (int i in values)
        {
            if (i == lowestValue)
                modeCount++;
        }
        
        if (modeCount > 1)
        {
            switch (modeConflictHandleType)
            {
                case ModeConflictHandleMethod.Random:
                    
                    List<int> modeIndexes = new List<int>();
                    
                    for (int i = 0; i < values.Count; i++)
                    {
                        if (values[i] == lowestValue)
                        {
                            modeIndexes.Add(i);
                        }
                    }
                
                    index = modeIndexes[Random.Range(0, modeIndexes.Count)];
                    break;
                
                case ModeConflictHandleMethod.FirstOption:
                    
                    index = values.IndexOf(lowestValue);
                    break;
                
                case ModeConflictHandleMethod.FastestReached:
                    
                    List<string> popularOptions = new List<string>();    //in this case, 'mode' and 'popular options' refer to the least abundant values
            
                    List<int> modeIndexes2 = new List<int>();
                    for (int i = 0; i < values.Count; i++)
                    {
                        if (values[i] == lowestValue)
                        {
                            modeIndexes2.Add(i);
                        }
                    }

                    for (int i = 0; i < modeIndexes2.Count; i++)
                    {
                        popularOptions.Add(options[modeIndexes2[i]]);
                    }

                    int[] latestOccurence = new int[popularOptions.Count];
                    
                    for (int i = 0; i < popularOptions.Count; i++)
                    {
                        foreach (string VARIABLE2 in storedChatTemp)
                        {
                            if (popularOptions[i] == VARIABLE2)
                            {
                                latestOccurence[i] = Array.IndexOf(storedChatTemp.ToArray(), VARIABLE2);
                            }
                        }
                    }
                    
                    int lowest = Mathf.Min(latestOccurence);
                    int h = Array.IndexOf(latestOccurence, lowest);
                    string s = popularOptions[h];

                    index = Array.IndexOf(options, s);
                    break;
                
                default:
                    //Runs FirstOption code by default, though this line should be impossible to reach
                    index = values.IndexOf(lowestValue);
                    break;
            }
        }
        else
        {
            index = values.IndexOf(lowestValue);
        }
        
        index = values.IndexOf(lowestValue);
        toReturn = options[index].ToString();

        return toReturn;
    }

    #endregion
    
    #region Utility Functions
    
    //Reset Twitch chat answers    
    private void ResetAnswers()
    {
        storedChatTemp.Clear();
        _submittedViewers.Clear();
    }
    
    //Returns the largest integer from a list
    private int LargestFromList(List<int> list)
    {
        return Mathf.Max(list.ToArray());
    }
    
    //Returnd the smallest integer from a list
    private int SmallestFromList(List<int> list)
    {
        return Mathf.Min(list.ToArray());
    }

    //Check if string exists in an array of strings
    private bool IfExistsInArray(string s, string[] a)
    {
        foreach (string option in a)
        {
            if (option == s)
            {
                return true;
            }
        }

        return false;
    }

    private bool IfViewerHasAnswered(string n)
    {
        foreach (string user in _submittedViewers)
            if (user == n)
                return true;
        
        return false;
    }
    
    #endregion
}
