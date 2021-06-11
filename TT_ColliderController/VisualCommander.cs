using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TT_ColliderController
{
    //WIP
    class VisualCommander
    {
        public class ModuleHideBlocks : TankBlock
        {
            //A Master controller that enables the hiding of blocks nearby while this block is active
            //  this is NOT the stealth module!  This just allows interiors of Techs to be explorable!
            //  It basically sets MeshRenderers' enabled to 'false'

            // Variables
            public bool DoNotHideBlock = false;//Set this to "true" to prevent block hiding
            private bool blockIsHidden = false;
        }
    }
}
