import React from "react";
import {Footage, FootageUrl} from "../../Models";
import {AjaxService} from "../../Services";

interface FootageListSnapProps
{
    footage: Footage;
    cameraName: string;
}

interface FootageListSnapState
{
    currentFootageIndex: number;
    footageUrl?: FootageUrl;
}

export class FootageListSnap extends React.Component<FootageListSnapProps, FootageListSnapState>
{
    constructor(props: FootageListSnapProps)
    {
        super(props);

        this.state = {
            currentFootageIndex: 0
        };
    }

    componentDidMount()
    {
        this.loadCurrentFootage();
    }

    private loadCurrentFootage()
    {
        const currentFootage = this.props.footage.sequences[this.state.currentFootageIndex];
        AjaxService.get('api/footage/' + this.props.cameraName + '?id=' + currentFootage.id).then((data: FootageUrl) =>
        {
            this.setState({
                footageUrl: data
            });
        });
    }

    render()
    {
        const currentFootage = this.props.footage.sequences[this.state.currentFootageIndex];
        let details = <></>;
        
        if (this.state.footageUrl)
        {
            details = <div className="card">
                <img className="card-img-top" src={this.state.footageUrl.url} alt={currentFootage.title}/>
                <div className="card-body">
                    <h5 className="card-title">{currentFootage.title}</h5>

                    <div className="row">
                        <div className="col-6">
                            <button type="button" className="btn btn-secondary w-100 p-2" onClick={(e) => this.moveFootage(e, -1)}>Previous</button>
                        </div>
                        <div className="col-6">
                            <button type="button" className="btn btn-primary w-100 p-2" onClick={(e) => this.moveFootage(e, 1)}>Next</button>
                        </div>
                    </div>
                </div>
            </div>;
        }
        
        return <>
            <div>
                <h3>
                    {this.props.footage.title}
                </h3>
                
                {details}
                
                
            </div>
        </>;
    }

    private moveFootage(e: React.MouseEvent<HTMLButtonElement>, jump: number)
    {
        e.preventDefault();
        
        const index = this.state.currentFootageIndex + jump;
        
        if (index < 0 || index >= this.props.footage.sequences.length)
        {
            return;
        }
        
        this.setState({currentFootageIndex: index});
        this.loadCurrentFootage();
    }
}