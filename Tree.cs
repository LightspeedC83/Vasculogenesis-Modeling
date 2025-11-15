using System;
using System.ComponentModel.DataAnnotations;
using System.Dynamic;
using System.Runtime.CompilerServices;
namespace VascularGenerator.DataStructures
{
    public class Tree<T> where T : IsCopyable<T>
    {   
        T value;
        List<Tree<T>> children;
        Tree<T> parent;

        static int IDMaster = 0;
        int ID;

        public Tree(T value)
        {
            this.value = value;
            children = new List<Tree<T>>();
            parent = null;
            ID = IDMaster;
            IDMaster += 1;
        }

        public void AddChild(Tree<T> child)
        {
            children.Add(child);
            child.parent = this;
        }

        public void SetChildren(List<Tree<T>> children)
        {
            this.children = children;
            foreach (Tree<T> child in children)
            {
                child.parent = this;
            }
        }

        public void SetParent(Tree<T> parent)
        {

            this.parent = parent;
            parent.AddChild(this);

        }

        public void SetValue(T value)
        {
            this.value = value;
        }

        public List<Tree<T>> GetChildren()
        {
            return children;
        }

        public Tree<T> GetParent()
        {
            return parent;
        }

        public T GetValue()
        {
            return value;
        }

        public int GetID()
        {
            return ID;
        }

        public bool IsLeaf()
        {
            return children.Count == 0;
        }

        public Tree<T> GetRoot()
        {
            if (this.parent == null)
            {
                return this;
            }
            else
            {
                return this.parent.GetRoot();
            }
        }
        
        public Tree<T> CreateCopy()
        {
            Tree<T> copy = new Tree<T>(value.CreateCopy());
            copy.ID = this.ID;
            if (this.children.Count == 0) //if there are no children, we return a copy of this node
            {
                return copy;
            }
            else //if there are children we create a copy of each child and add them as children to the copy of this node, then return the copy of this node
            {
                foreach (Tree<T> c in this.children)
                {
                    copy.AddChild(c.CreateCopy());
                }
                return copy;
            }
        }

        public override string ToString()
        {
            string output = "Tree Node:\n"+ value.ToString();
            if (children.Count == 0)
            {
                return value.ToString() + "\n--noChildren--";
            }
            else
            {
                foreach (Tree<T> c in children)
                {
                    output += "\n\tChild:\n" + c.ToString();
                }
            }
            return output;
        }
    }
}
