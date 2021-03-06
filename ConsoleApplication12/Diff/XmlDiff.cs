﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace ConsoleApplication12.Diff
{
    class XMLDiff
    {
        // V hashMape su ulozene vsetky elementy 1. verzie kodu v XML
        private  Dictionary<int, XmlNode> _hashMap1;

        // V hashMape su ulozene vsetky elementy 2. verzie kodu v XML
        private  Dictionary<int, XmlNode> _hashMap2;

        // Pomocna hashMapa ukladam sem tie zmazane elementy z 1. verzie ktore boli prekopirovane
        private  Dictionary<int, XmlNode> _hashMap3;

        // Zaloha originalnej hash mapy 1
        private  Dictionary<int, XmlNode> _hashMap4;

        private  XmlNode root;

        public XMLDiff()
        {
            _hashMap1 = new Dictionary<int, XmlNode>();
            _hashMap2 = new Dictionary<int, XmlNode>();
            _hashMap3 = new Dictionary<int, XmlNode>();
            _hashMap4 = new Dictionary<int, XmlNode>();
        }

        public  void DiffXmlFiles(String fileName1, String fileName2)
        {

            XmlDocument doc1 = new XmlDocument();
            doc1.Load(fileName1);

            XmlDocument doc2 = new XmlDocument();
            doc2.Load(fileName2);

            root = doc1.ChildNodes.Item(0);

            // Zahesuje vsetky XML elementy pveho dokumentu
            Hashing(doc1.ChildNodes, _hashMap1);

            foreach (KeyValuePair<int, XmlNode> entry in _hashMap1)
            {
                _hashMap4.Add(entry.Key, (XmlNode)entry.Value.Clone());
            }

            // Zahesuje vsetky XML elementy druheho dokumentu
            Hashing(doc2.ChildNodes, _hashMap2);

            MakeDiff(doc2.ChildNodes, doc2);

            findRemovedItems(doc2);

            doc2.Save("difference.xml");

        }

        // Najde elementy ktore boli vymazane v druhej verzii
        private  void findRemovedItems(XmlDocument doc2)
        {
            foreach (KeyValuePair<int, XmlNode> entry in _hashMap1)
            {
                XmlNode tempNode = entry.Value;
                XmlNode fromHashTable;
                if (!_hashMap3.TryGetValue(entry.Key, out fromHashTable))
                    FindParent(tempNode, doc2);
            }
        }

        // Prechadza rodicovskymi elementmi a nastavuje status na deleted
        private  void SetStatusDeleted(XmlNodeList list, XmlDocument doc1)
        {
            foreach (XmlNode listNode in list)
            {
                if (listNode.NodeType == XmlNodeType.Element)
                {
                    XmlNode outOfMapNode;
                    XmlNode idAtrib = listNode.Attributes.GetNamedItem("id");

                    if (!_hashMap2.TryGetValue(Convert.ToInt32(idAtrib.Value), out outOfMapNode))
                    {
                        XmlAttribute statusAtrib = doc1.CreateAttribute("diff","status", "http://www.via.ecp.fr/~remi/soft/xml/xmldiff");
                        statusAtrib.Value = "removed";
                        listNode.Attributes.Append(statusAtrib);
                    }
                    if (listNode.HasChildNodes)
                        SetStatusDeleted(listNode.ChildNodes, doc1);
                }
            }
        }

        // Hladada najblizsieho nezmazaneho rodica
        private  void FindParent(XmlNode node, XmlDocument doc2)
        {
            XmlNode tempNode;
            XmlNode child = null;

            var idAtrib = node.Attributes.GetNamedItem("id");
            while (!_hashMap2.TryGetValue(Convert.ToInt32(idAtrib.Value), out tempNode))
            {
                child = node;
                node = node.ParentNode;
                idAtrib = node.Attributes.GetNamedItem("id");

            }

            XmlAttribute status = doc2.CreateAttribute("diff","status", "http://www.via.ecp.fr/~remi/soft/xml/xmldiff");
            status.Value = "removed";

            foreach (XmlNode childNode in node.ChildNodes)
            {
                if (childNode == child)
                {
                    XmlNode importNode = tempNode.OwnerDocument.ImportNode(childNode, true);
                    SetStatusDeleted(importNode.ChildNodes, doc2);
                    SetParentDiffStatus(doc2, importNode, root);

                    // Delete added node and his children from HashMap
                    _hashMap3.Add(Convert.ToInt32(importNode.Attributes.GetNamedItem("id").Value), importNode);
                    Hashing(importNode.ChildNodes, _hashMap3);

                    if (XmlNodeType.Element == childNode.NodeType)
                    {
                        importNode.Attributes.Append(status);
                    }
                    tempNode.AppendChild(importNode);
                }
            }
        }

        /*private  void HashDeletition(XmlNodeList list1, Dictionary<int, XmlNode> hashMap)
        {
            foreach (XmlNode listNode in list1)
            {
                if (listNode.NodeType == XmlNodeType.Element)
                {
                    var idAtrib = listNode.Attributes.GetNamedItem("id");
                    if (idAtrib != null)
                    {
                        hashMap.Remove(Convert.ToInt32(idAtrib.Value));
                    }
                    if (listNode.HasChildNodes)
                        HashDeletition(listNode.ChildNodes, hashMap);
                }
            }
        }*/

        // Vytvori hash mapu s listu xml elementov
        private  void Hashing(XmlNodeList list1, Dictionary<int, XmlNode> hashMap)
        {
            foreach (XmlNode listNode in list1)
            {
                if (listNode.NodeType == XmlNodeType.Element)
                {
                    var idAtrib = listNode.Attributes.GetNamedItem("id");
                    if (idAtrib != null)
                    {
                        hashMap.Add(Convert.ToInt32(idAtrib.Value), listNode);
                    }
                    if (listNode.HasChildNodes)
                        Hashing(listNode.ChildNodes, hashMap);
                }
            }

        }

        // Vytvara cast diff suboru kde su oznacene pridane a modifikovane elementy
        private  void MakeDiff(XmlNodeList list1, XmlDocument doc1)
        {
            foreach (XmlNode listNode in list1)
            {
                if (listNode.NodeType == XmlNodeType.Element)
                {
                    var idAtrib = listNode.Attributes.GetNamedItem("id");
                    if (idAtrib != null)
                    {
                        XmlNode tempNode;
                        if (_hashMap1.TryGetValue(Convert.ToInt32(idAtrib.Value), out tempNode))
                        {
                            if (listNode.InnerText != tempNode.InnerText)
                            {
                                //listNode.InnerText += "~" + tempNode.InnerText;
                                XmlAttribute diffAtrib = doc1.CreateAttribute("diff","status", "http://www.via.ecp.fr/~remi/soft/xml/xmldiff");
                                diffAtrib.Value = "modified";

                                XmlNodeList beforeList = tempNode.SelectNodes("child::text()");
                                XmlNodeList afterList = listNode.SelectNodes("child::text()");

                                bool modification = false;
                                int count = 0;
                                while(afterList.Count > count || beforeList.Count > count)
                                //for (int i = 0; i < beforeList.Count; i++)
                                {
                                    if (afterList.Count > count)
                                    {
                                        if (beforeList.Count > count)
                                        {
                                            if (beforeList[count].Value != afterList[count].Value)
                                            {
                                                afterList[count].Value = beforeList[count].Value + "~" + afterList[count].Value;
                                                modification = true;
                                            }
                                        }
                                        else
                                        {
                                            afterList[count].Value = "~" + afterList[count].Value;
                                            modification = true;
                                        }
                                    }
                                    else
                                    {
                                        beforeList[count].Value += "~";
                                        modification = true;
                                    }

                                    count++;
                                }

                                if (!modification)
                                    diffAtrib.Value = "below";

                                listNode.Attributes.Append(diffAtrib);
                                SetParentDiffStatus(doc1, listNode, root);

                            }

                            _hashMap1.Remove(Convert.ToInt32(idAtrib.Value));
                        }
                        else
                        {
                            XmlAttribute diffAtrib = doc1.CreateAttribute("diff","status", "http://www.via.ecp.fr/~remi/soft/xml/xmldiff");
                            diffAtrib.Value = "added";

                            // Nastavim hodnotu atributu status a prechadzam postupne az na posledneho rodica ktory nema nastaveny status

                            listNode.Attributes.Append(diffAtrib);
                            SetParentDiffStatusAdded(doc1, listNode, root, _hashMap1);
                        }

                    }

                    if (listNode.HasChildNodes)
                        MakeDiff(listNode.ChildNodes, doc1);
                }
                /*if(listNode.NodeType == XmlNodeType.Text)
                {
                    int i = 0;
                }*/
            }
        }

        // Prechadza cez rodicov a nastavuje diff status
        private  void SetParentDiffStatus(XmlDocument doc1, XmlNode node, XmlNode root)
        {
            node = node.ParentNode;
            while (node != null && node.ParentNode != root)
            {
                if (node.NodeType == XmlNodeType.Element)
                {
                    XmlNode diffAtrib = node.Attributes.GetNamedItem("status", "http://www.via.ecp.fr/~remi/soft/xml/xmldiff");

                    if (diffAtrib == null)
                    {
                        XmlAttribute diffAtribute = doc1.CreateAttribute("diff","status", "http://www.via.ecp.fr/~remi/soft/xml/xmldiff");
                        diffAtribute.Value = "below";
                        node.Attributes.Append(diffAtribute);
                    }
                    //else
                      //  diffAtrib.Value = "below";

                }
                node = node.ParentNode;
            }
        }

        // Prechadza cez rodicov a nastavuje diff status na added
        private  void SetParentDiffStatusAdded(XmlDocument doc1, XmlNode node, XmlNode root, Dictionary<int, XmlNode> hashMap)
        {
            node = node.ParentNode;
            while (node != null && node.ParentNode != root)
            {
                if (node.NodeType == XmlNodeType.Element)
                {
                    XmlNode outTableNode;
                    XmlNode diffAtrib = node.Attributes.GetNamedItem("status", "http://www.via.ecp.fr/~remi/soft/xml/xmldiff");
                    XmlNode idAtrib = node.Attributes.GetNamedItem("id");
                    String status = "below";

                    if (!_hashMap4.TryGetValue(Convert.ToInt32(idAtrib.Value), out outTableNode))
                        status = "added";

                    if (diffAtrib == null)
                    {
                        XmlAttribute diffAtribute = doc1.CreateAttribute("diff", "status", "http://www.via.ecp.fr/~remi/soft/xml/xmldiff");
                        diffAtribute.Value = status;
                        node.Attributes.Append(diffAtribute);
                    }
                    else
                        diffAtrib.Value = status;

                }
                node = node.ParentNode;
            }
        }


        /* void Main(string[] args)
        {
            diffXmlFiles("source_data1.xml", "source_data2.xml");
        }*/
    }
}
