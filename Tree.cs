using System;
using System.ComponentModel.DataAnnotations;
namespace VascularGenerator.DataStructures
{
    public class Tree<T>
    {
        T value;
        List<Tree<T>> children;
        Tree<T> parent;

        public Tree(T value)
        {
            this.value = value;
            children = new List<Tree<T>>();
            parent = null;
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

        public bool IsLeaf()
        {
            return children.Count == 0;
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
